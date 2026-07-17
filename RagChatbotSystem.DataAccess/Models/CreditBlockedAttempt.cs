using System;

namespace RagChatbotSystem.DataAccess.Models
{
    public class CreditBlockedAttempt
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? DatasetId { get; set; }
        public Guid? ChatSessionId { get; set; }
        public CreditBlockedReason Reason { get; set; }
        public int FreeCreditsAtTime { get; set; }
        public int PaidCreditsAtTime { get; set; }
        public int? UsedTokensToday { get; set; }
        public int? DailyTokenLimit { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? MessagePreview { get; set; }
        public string? Note { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual Dataset? Dataset { get; set; }
        public virtual ChatSession? ChatSession { get; set; }
    }
}
