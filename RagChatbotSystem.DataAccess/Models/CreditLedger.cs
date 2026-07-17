using System;

namespace RagChatbotSystem.DataAccess.Models
{
    public class CreditLedger
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? DatasetId { get; set; }
        public Guid? ChatSessionId { get; set; }
        public Guid? ChatMessageId { get; set; }
        public CreditLedgerType Type { get; set; }
        public int CalculatedCredits { get; set; }
        public int ChargedCredits { get; set; }
        public int FreeCreditsUsed { get; set; }
        public int PaidCreditsUsed { get; set; }
        public int FreeCreditsAdded { get; set; }
        public int PaidCreditsAdded { get; set; }
        public int BalanceBeforeFree { get; set; }
        public int BalanceBeforePaid { get; set; }
        public int BalanceAfterFree { get; set; }
        public int BalanceAfterPaid { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens { get; set; }
        public int OutputTokenWeight { get; set; }
        public int TokenUnit { get; set; }
        public string? ModelName { get; set; }
        public bool WasActualTokenUsage { get; set; }
        public bool WasInsufficientBalance { get; set; }
        public Guid? RelatedPackageId { get; set; }
        public string? Note { get; set; }
        public Guid? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
        public virtual Dataset? Dataset { get; set; }
        public virtual ChatSession? ChatSession { get; set; }
        public virtual ChatMessage? ChatMessage { get; set; }
        public virtual CreditPackage? RelatedPackage { get; set; }
        public virtual User? CreatedByUser { get; set; }
    }
}
