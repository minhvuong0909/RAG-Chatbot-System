using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    public class ChatSession
    {
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public Guid DatasetId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Dataset Dataset { get; set; } = null!;
        public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    }
}
