using System;

namespace RagChatbotSystem.DataAccess.Models
{
    public class UserTokenUsage
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid DatasetId { get; set; }
        public DateTime Date { get; set; }
        public int TokenCount { get; set; }
        public int QueryCount { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual Dataset Dataset { get; set; } = null!;
    }
}
