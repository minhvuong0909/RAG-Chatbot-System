using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Realtime;

namespace RagChatbotSystem.Presentation.Pages.Api
{
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public sealed class PayOsWebhookModel : PageModel
    {
        private readonly IPayOsService _payOsService;
        private readonly ICreditPurchaseService _purchaseService;
        private readonly ILogger<PayOsWebhookModel> _logger;
        private readonly ICreditService _creditService;
        private readonly IRealtimeNotifier _realtimeNotifier;

        public PayOsWebhookModel(IPayOsService payOsService, ICreditPurchaseService purchaseService, ICreditService creditService, IRealtimeNotifier realtimeNotifier, ILogger<PayOsWebhookModel> logger)
        {
            _payOsService = payOsService;
            _purchaseService = purchaseService;
            _creditService = creditService;
            _realtimeNotifier = realtimeNotifier;
            _logger = logger;
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        {
            JsonDocument document;
            try
            {
                document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                return BadRequest(new { success = false, message = "Dữ liệu webhook không hợp lệ." });
            }

            using (document)
            {
                var webhook = _payOsService.VerifyWebhook(document.RootElement);
                if (!webhook.IsValid)
                {
                    _logger.LogWarning("Rejected PayOS webhook: {Reason}", webhook.ErrorMessage);
                    return BadRequest(new { success = false, message = webhook.ErrorMessage });
                }

                // payOS sends a signed sample payload when registering the webhook.
                var purchase = await _purchaseService.GetPurchaseByOrderCodeAsync(webhook.OrderCode, cancellationToken);
                if (purchase == null)
                {
                    _logger.LogInformation("Accepted PayOS verification webhook for unknown/sample order {OrderCode}.", webhook.OrderCode);
                    return new JsonResult(new { success = true });
                }

                if (webhook.IsSuccessful)
                {
                    var completed = await _purchaseService.CompletePayOsPurchaseAsync(
                        webhook.OrderCode,
                        webhook.Amount,
                        webhook.Reference,
                        cancellationToken);
                    var balance = await _creditService.GetStudentCreditSummaryAsync(completed.UserId, cancellationToken);
                    await _realtimeNotifier.CreditBalanceChangedAsync(completed.UserId, balance, "payos-purchase", cancellationToken);
                }

                return new JsonResult(new { success = true });
            }
        }
    }
}
