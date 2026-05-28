using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    public class Chunk
    {
        public Guid ChunkId { get; set; }
        public Guid DatasetId { get; set; }
        public Guid DocumentId { get; set; }
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public string? SectionTitle { get; set; }
        public string? MetadataJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Dataset Dataset { get; set; } = null!;
        public virtual Document Document { get; set; } = null!;
        public virtual VectorRecord? VectorRecord { get; set; }
        public virtual ICollection<Citation> Citations { get; set; } = new List<Citation>();
    }
}
