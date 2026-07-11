using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Pages.Admin.Credits
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ICreditService _creditService;
        private readonly ICreditPurchaseService _purchaseService;

        public IndexModel(IUserService userService, ICreditService creditService, ICreditPurchaseService purchaseService)
        {
            _userService = userService;
            _creditService = creditService;
            _purchaseService = purchaseService;
        }

        public IReadOnlyList<UserDto> Students { get; private set; } = Array.Empty<UserDto>();
        public CreditBalanceDto? SelectedBalance { get; private set; }
        public IReadOnlyList<CreditLedgerDto> Ledger { get; private set; } = Array.Empty<CreditLedgerDto>();
        public IReadOnlyList<CreditPackageDto> Packages { get; private set; } = Array.Empty<CreditPackageDto>();
        public IReadOnlyList<CreditPurchaseDto> Purchases { get; private set; } = Array.Empty<CreditPurchaseDto>();
        public IReadOnlyDictionary<Guid, string> UserNames { get; private set; } = new Dictionary<Guid, string>();
        public Guid? SelectedUserId { get; private set; }

        [BindProperty]
        public CreditActionInput Input { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync(Guid? userId, CancellationToken cancellationToken)
        {
            await LoadAsync(userId, cancellationToken);
        }

        public async Task<IActionResult> OnPostTopUpAsync(CancellationToken cancellationToken)
        {
            if (!TryGetAdminId(out var adminId))
            {
                return Challenge();
            }

            try
            {
                await _purchaseService.CreateManualTopUpAsync(Input.UserId, Input.PaidCredits, Input.Amount, "VND", adminId, Input.Note, cancellationToken);
                SuccessMessage = "Đã cộng Credit đã mua cho sinh viên.";
            }
            catch (Exception)
            {
                ErrorMessage = "Không thể cộng Credit đã mua. Vui lòng kiểm tra thông tin và thử lại.";
            }

            return RedirectToPage(new { userId = Input.UserId });
        }

        public async Task<IActionResult> OnPostGrantFreeAsync(CancellationToken cancellationToken)
        {
            if (!TryGetAdminId(out var adminId))
            {
                return Challenge();
            }

            try
            {
                await _creditService.GrantFreeCreditsAsync(Input.UserId, Input.FreeCredits, adminId, Input.Note, cancellationToken);
                SuccessMessage = "Đã tặng Credit miễn phí cho sinh viên.";
            }
            catch (Exception)
            {
                ErrorMessage = "Không thể tặng Credit miễn phí. Vui lòng kiểm tra thông tin và thử lại.";
            }

            return RedirectToPage(new { userId = Input.UserId });
        }

        public async Task<IActionResult> OnPostAdjustAsync(CancellationToken cancellationToken)
        {
            if (!TryGetAdminId(out var adminId))
            {
                return Challenge();
            }

            try
            {
                await _creditService.AdjustCreditsAsync(Input.UserId, Input.FreeCreditsDelta, Input.PaidCreditsDelta, adminId, Input.Note, cancellationToken);
                SuccessMessage = "Đã điều chỉnh số dư Credit.";
            }
            catch (Exception)
            {
                ErrorMessage = "Không thể điều chỉnh số dư Credit. Vui lòng kiểm tra thông tin và thử lại.";
            }

            return RedirectToPage(new { userId = Input.UserId });
        }

        private async Task LoadAsync(Guid? userId, CancellationToken cancellationToken)
        {
            var users = await _userService.GetUsersAsync(cancellationToken);
            UserNames = users.ToDictionary(user => user.UserId, user => user.FullName);
            Students = users
                .Where(u => string.Equals(u.Role, "Student", StringComparison.OrdinalIgnoreCase))
                .OrderBy(u => u.FullName)
                .ToList();
            Packages = await _purchaseService.GetPackagesAsync(cancellationToken);
            SelectedUserId = userId ?? Students.FirstOrDefault()?.UserId;

            if (SelectedUserId.HasValue)
            {
                SelectedBalance = await _creditService.GetStudentCreditSummaryAsync(SelectedUserId.Value, cancellationToken);
                Ledger = await _creditService.GetLedgerAsync(SelectedUserId.Value, 50, cancellationToken);
                Purchases = await _purchaseService.GetPurchasesAsync(SelectedUserId.Value, 50, cancellationToken);
                Input.UserId = SelectedUserId.Value;
            }
        }

        private bool TryGetAdminId(out Guid adminId)
        {
            return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out adminId);
        }

        public class CreditActionInput
        {
            [Required]
            public Guid UserId { get; set; }

            [Range(1, 1000000)]
            public int PaidCredits { get; set; }

            [Range(0, 100000000)]
            public decimal Amount { get; set; }

            [Range(1, 1000000)]
            public int FreeCredits { get; set; }

            public int FreeCreditsDelta { get; set; }
            public int PaidCreditsDelta { get; set; }

            [Required]
            [StringLength(500)]
            public string Note { get; set; } = string.Empty;
        }
    }
}
