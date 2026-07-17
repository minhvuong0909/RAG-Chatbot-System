using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    public class BenchmarkQuestion
    {
        public Guid Id { get; set; }
        public Guid BenchmarkDefinitionId { get; set; }
        public int SortOrder { get; set; }
        public string Question { get; set; } = string.Empty;
        public string ReferenceAnswer { get; set; } = string.Empty;
        public string? EvidenceNote { get; set; }
        public string? RelevantChunkIdsJson { get; set; }
        public string? SourceReference { get; set; }
        public string Category { get; set; } = "fact";
        public bool IsHoldout { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual BenchmarkDefinition BenchmarkDefinition { get; set; } = null!;
        public virtual ICollection<EvaluationResult> Results { get; set; } = new List<EvaluationResult>();
    }
}
