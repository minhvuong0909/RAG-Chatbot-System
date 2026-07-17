using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    public class ModelComparisonRun
    {
        public Guid Id { get; set; }
        public Guid DatasetId { get; set; }
        public Guid RunByUserId { get; set; }
        public string Question { get; set; } = string.Empty;
        public int RetrievedChunkCount { get; set; }
        public long RetrievalLatencyMs { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Dataset Dataset { get; set; } = null!;
        public virtual User RunByUser { get; set; } = null!;
        public virtual ICollection<ModelComparisonResult> Results { get; set; } = new List<ModelComparisonResult>();
    }
}
