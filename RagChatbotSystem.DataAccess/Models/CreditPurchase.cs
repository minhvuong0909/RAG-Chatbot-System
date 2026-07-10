using System;

namespace RagChatbotSystem.DataAccess.Models
{
    public class CreditPurchase
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? PackageId { get; set; }
        public int BaseCredits { get; set; }
        public int BonusCredits { get; set; }
        public int TotalCredits { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public CreditPurchaseStatus Status { get; set; } = CreditPurchaseStatus.PENDING;
        public string? PaymentProvider { get; set; }
        public string? ProviderReference { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public Guid? CreatedByUserId { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual CreditPackage? Package { get; set; }
        public virtual User? CreatedByUser { get; set; }
    }
}
