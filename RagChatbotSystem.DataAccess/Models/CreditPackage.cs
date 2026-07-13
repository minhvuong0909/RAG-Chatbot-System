using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    public class CreditPackage
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int BaseCredits { get; set; }
        public int BonusCredits { get; set; }
        public int TotalCredits { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "VND";
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
        public int? ValidDays { get; set; }
        public int? ExpiresAfterDays { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<CreditPurchase> Purchases { get; set; } = new List<CreditPurchase>();
        public virtual ICollection<CreditLedger> LedgerEntries { get; set; } = new List<CreditLedger>();
    }
}
