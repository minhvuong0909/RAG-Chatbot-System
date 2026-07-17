using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    public class EvaluationResult
    {
        public Guid Id { get; set; }
        public Guid EvaluationRunId { get; set; }
        public Guid BenchmarkQuestionId { get; set; }
        public string Answer { get; set; } = string.Empty;
        public long RetrievalLatencyMs { get; set; }
        public long GenerationLatencyMs { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens { get; set; }
        public string Status { get; set; } = "Pending";
        public string? ErrorMessage { get; set; }
        public double? Faithfulness { get; set; }
        public double? AnswerRelevancy { get; set; }
        public double? ContextPrecision { get; set; }
        public double? ContextRecall { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual EvaluationRun EvaluationRun { get; set; } = null!;
        public virtual BenchmarkQuestion BenchmarkQuestion { get; set; } = null!;
        public virtual ICollection<EvaluationEvidence> Evidence { get; set; } = new List<EvaluationEvidence>();
    }
}
