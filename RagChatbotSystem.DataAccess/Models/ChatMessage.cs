using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    public class ChatMessage
    {
        public Guid MessageId { get; set; }
        public Guid SessionId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ChatSession ChatSession { get; set; } = null!;
        public virtual ICollection<Citation> Citations { get; set; } = new List<Citation>();
    }
}
