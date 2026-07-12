namespace RagChatbotSystem.Business.DTOs
{
    public sealed record PayOsCheckoutResult(
        long OrderCode,
        string PaymentLinkId,
        string CheckoutUrl,
        string QrCode);

    public sealed record PayOsWebhookResult(
        bool IsValid,
        bool IsSuccessful,
        long OrderCode,
        decimal Amount,
        string Reference,
        string? ErrorMessage = null);

    /// <summary>
    /// Kết quả query trạng thái thanh toán từ PayOS API.
    /// Status: "PAID" | "PENDING" | "PROCESSING" | "CANCELLED" | "EXPIRED"
    /// </summary>
    public sealed record PayOsPaymentStatusResult(
        bool IsSuccess,
        string Status,
        long OrderCode,
        decimal AmountPaid,
        string? Reference,
        string? ErrorMessage = null);
}
