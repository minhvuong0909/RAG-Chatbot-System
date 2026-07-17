using System;

namespace RagChatbotSystem.DataAccess.Models
{
    public class CreditWallet
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public int FreeCredits { get; set; }
        public int PaidCredits { get; set; }
        public DateTime LastFreeCreditResetDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public uint Version { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
