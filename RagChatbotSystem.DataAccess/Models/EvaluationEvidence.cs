using System;

namespace RagChatbotSystem.DataAccess.Models
{
    /// <summary>Exact retrieved context persisted for audit and RAGAS input.</summary>
    public class EvaluationEvidence
    {
        public Guid Id { get; set; }
        public Guid EvaluationResultId { get; set; }
        public Guid? ChunkId { get; set; }
        public int Rank { get; set; }
        public double? Score { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? MetadataJson { get; set; }

        public virtual EvaluationResult EvaluationResult { get; set; } = null!;
        public virtual Chunk? Chunk { get; set; }
    }
}
