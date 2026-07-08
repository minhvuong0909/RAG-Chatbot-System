using System;

namespace RagChatbotSystem.DataAccess.Models
{
    public class UserTokenUsage
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime Date { get; set; }
        public int TokenCount { get; set; }
        public int QueryCount { get; set; }

        // Navigation property
        public virtual User User { get; set; } = null!;
    }
}
