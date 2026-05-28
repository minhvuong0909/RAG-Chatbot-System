using System;

namespace RagChatbotSystem.DataAccess.Models
{
    public class Citation
    {
        public Guid CitationId { get; set; }
        public Guid MessageId { get; set; }
        public Guid ChunkId { get; set; }
        public Guid DocumentId { get; set; }
        public int PageNumber { get; set; }
        public string QuoteText { get; set; } = string.Empty;
        public string? SourceLabel { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ChatMessage ChatMessage { get; set; } = null!;
        public virtual Chunk Chunk { get; set; } = null!;
        public virtual Document Document { get; set; } = null!;
    }
}
