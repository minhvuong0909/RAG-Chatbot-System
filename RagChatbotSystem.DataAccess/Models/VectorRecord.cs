using System;
using Pgvector;

namespace RagChatbotSystem.DataAccess.Models
{
    public class VectorRecord
    {
        public Guid VectorId { get; set; }
        public Guid DatasetId { get; set; }
        public Guid DocumentId { get; set; }
        public Guid ChunkId { get; set; }
        public Vector Embedding { get; set; } = null!;
        public string EmbeddingModel { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Dataset Dataset { get; set; } = null!;
        public virtual Document Document { get; set; } = null!;
        public virtual Chunk Chunk { get; set; } = null!;
    }
}
