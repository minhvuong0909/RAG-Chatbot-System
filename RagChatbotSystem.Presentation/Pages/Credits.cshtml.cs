using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Realtime;

namespace RagChatbotSystem.Presentation.Pages
{
    [Authorize(Roles = "Student")]
    public class CreditsModel : PageModel
    {
        private readonly ICreditService _creditService;
        private readonly ICreditPurchaseService _purchaseService;
        private readonly IPayOsService _payOsService;
        private readonly IConfiguration _configuration;
        private readonly IRealtimeNotifier _realtimeNotifier;

        public CreditsModel(ICreditService creditService, ICreditPurchaseService purchaseService, IPayOsService payOsService, IConfiguration configuration, IRealtimeNotifier realtimeNotifier)
        {
            _creditService = creditService;
            _purchaseService = purchaseService;
            _payOsService = payOsService;
            _configuration = configuration;
            _realtimeNotifier = realtimeNotifier;
        }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public bool IsPayOsConfigured => _payOsService.IsConfigured;

        public CreditBalanceDto Balance { get; private set; } = null!;
        public IReadOnlyList<CreditPackageDto> Packages { get; private set; } = Array.Empty<CreditPackageDto>();
        public IReadOnlyList<CreditLedgerDto> Ledger { get; private set; } = Array.Empty<CreditLedgerDto>();
        public IReadOnlyList<CreditPurchaseDto> Purchases { get; private set; } = Array.Empty<CreditPurchaseDto>();

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Challenge();
            }

            var creditBalanceChanged = false;
            var paymentResult = Request.Query["payment"].ToString();
            if (string.Equals(paymentResult, "return", StringComparison.OrdinalIgnoreCase))
            {
                // PayOS tự append orderCode vào returnUrl khi redirect, dùng để query trực tiếp thay webhook
                if (long.TryParse(Request.Query["orderCode"], out var returnOrderCode))
                {
                    try
                    {
                        var purchase = await _purchaseService.GetPurchaseByOrderCodeAsync(returnOrderCode, cancellationToken);
                        if (purchase != null && purchase.UserId == userId)
                        {
                            if (purchase.Status == RagChatbotSystem.DataAccess.Models.CreditPurchaseStatus.COMPLETED)
                            {
                                // Đã complete rồi (ví dụ webhook kịp gọi trước) — chỉ cần thông báo
                                SuccessMessage = "Thanh toán thành công! Credit đã được cộng vào tài khoản.";
                            }
                            else if (purchase.Status == RagChatbotSystem.DataAccess.Models.CreditPurchaseStatus.PENDING)
                            {
                                // Query PayOS API để lấy trạng thái thực tế
                                var status = await _payOsService.GetPaymentStatusAsync(returnOrderCode, cancellationToken);
                                if (status.IsSuccess && string.Equals(status.Status, "PAID", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Dùng amountPaid từ PayOS; nếu bằng 0 (sandbox quirk) thì fallback về purchase.Amount
                                    var paidAmount = status.AmountPaid > 0 ? status.AmountPaid : purchase.Amount;
                                    await _purchaseService.CompletePayOsPurchaseAsync(
                                        returnOrderCode,
                                        paidAmount,
                                        status.Reference ?? string.Empty,
                                        cancellationToken);
                                    creditBalanceChanged = true;
                                    SuccessMessage = "Thanh toán thành công! Credit đã được cộng vào tài khoản.";
                                }
                                else
                                {
                                    // Chưa PAID (đang xử lý / timeout) — để người dùng biết
                                    SuccessMessage = $"Đã nhận yêu cầu thanh toán (trạng thái: {(status.IsSuccess ? status.Status : "Đang xử lý")}). " +
                                                     "Credit sẽ được cộng tự động khi thanh toán hoàn tất.";
                                }
                            }
                        }
                        else
                        {
                            SuccessMessage = "PayOS đã tiếp nhận thanh toán. Hệ thống sẽ xác nhận và cộng Credit sớm.";
                        }
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
                    {
                        // CompletePayOsPurchaseAsync ném exception khi amount không khớp hoặc trạng thái lỗi
                        ErrorMessage = $"Lỗi xác nhận thanh toán: {ex.Message}";
                    }
                    catch
                    {
                        // Lỗi network / timeout khi gọi PayOS API — không crash trang
                        SuccessMessage = "PayOS đã tiếp nhận thanh toán. Hệ thống sẽ xác nhận và cộng Credit sớm.";
                    }
                }
                else
                {
                    // Không có orderCode trong URL (không bình thường) — thông báo chung
                    SuccessMessage = "PayOS đã tiếp nhận thanh toán. Hệ thống sẽ cộng Credit ngay sau khi webhook được xác minh.";
                }
            }
            else if (string.Equals(paymentResult, "cancel", StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(Request.Query["orderCode"], out var orderCode))
                {
                    var purchase = await _purchaseService.GetPurchaseByOrderCodeAsync(orderCode, cancellationToken);
                    if (purchase?.UserId == userId)
                    {
                        await _purchaseService.CancelPayOsPurchaseAsync(orderCode, cancellationToken);
                    }
                }
                ErrorMessage = "Bạn đã hủy thanh toán PayOS. Giao dịch chưa làm thay đổi số dư Credit.";
            }

            Balance = await _creditService.GetStudentCreditSummaryAsync(userId, cancellationToken);
            if (creditBalanceChanged)
            {
                await _realtimeNotifier.CreditBalanceChangedAsync(userId, Balance, "payos-purchase", cancellationToken);
            }
            Packages = await _purchaseService.GetActivePackagesAsync(cancellationToken);
            Ledger = await _creditService.GetLedgerAsync(userId, 25, cancellationToken);
            Purchases = await _purchaseService.GetPurchasesAsync(userId, 25, cancellationToken);
            return Page();
        }

        public async Task<IActionResult> OnPostBuyAsync(Guid packageId, CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Challenge();
            if (!_payOsService.IsConfigured)
            {
                ErrorMessage = "PayOS chưa được cấu hình. Vui lòng liên hệ quản trị viên.";
                return RedirectToPage();
            }

            try
            {
                var purchase = await _purchaseService.CreatePurchaseAsync(userId, packageId, cancellationToken);
                var configuredBaseUrl = _configuration["PayOs:PublicBaseUrl"]?.TrimEnd('/');
                var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
                    ? $"{Request.Scheme}://{Request.Host}"
                    : configuredBaseUrl;
                var checkout = await _payOsService.CreatePaymentLinkAsync(
                    purchase,
                    string.Empty,
                    $"{baseUrl}/Credits?payment=return",
                    $"{baseUrl}/Credits?payment=cancel",
                    cancellationToken);
                await _purchaseService.AttachPaymentLinkAsync(
                    purchase.Id,
                    checkout.OrderCode,
                    checkout.PaymentLinkId,
                    checkout.CheckoutUrl,
                    cancellationToken);
                return Redirect(checkout.CheckoutUrl);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return RedirectToPage();
            }
        }
    }
}
