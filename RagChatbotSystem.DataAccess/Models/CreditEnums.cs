namespace RagChatbotSystem.DataAccess.Models
{
    public enum CreditLedgerType
    {
        FREE_GRANT,
        DAILY_RESET,
        PURCHASE,
        ADMIN_GRANT,
        ADJUSTMENT,
        SPEND,
        REFUND,
        EXPIRED
    }

    public enum CreditPurchaseStatus
    {
        PENDING,
        COMPLETED,
        FAILED,
        CANCELLED,
        REFUNDED
    }

    public enum CreditBlockedReason
    {
        ZERO_BALANCE,
        DAILY_TOKEN_LIMIT,
        MODEL_LIMIT,
        PROVIDER_ERROR
    }
}
