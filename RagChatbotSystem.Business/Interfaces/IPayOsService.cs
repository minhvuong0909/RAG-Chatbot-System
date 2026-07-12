using System.Text.Json;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IPayOsService
    {
        bool IsConfigured { get; }
        Task<PayOsCheckoutResult> CreatePaymentLinkAsync(
            CreditPurchaseDto purchase,
            string description,
            string returnUrl,
            string cancelUrl,
            CancellationToken cancellationToken = default);
        PayOsWebhookResult VerifyWebhook(JsonElement payload);

        /// <summary>
        /// Query trực tiếp PayOS API để lấy trạng thái đơn hàng.
        /// Dùng khi không có webhook (môi trường dev/local).
        /// </summary>
        Task<PayOsPaymentStatusResult> GetPaymentStatusAsync(long orderCode, CancellationToken cancellationToken = default);
    }
}
