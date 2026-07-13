using System;

namespace RagChatbotSystem.DataAccess.Models
{
    public class ModelComparisonResult
    {
        public Guid Id { get; set; }
        public Guid ModelComparisonRunId { get; set; }
        public string ProviderKey { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public long LatencyMs { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public int? QualityScore { get; set; }
        public string? QualityReasoning { get; set; }

        public virtual ModelComparisonRun Run { get; set; } = null!;
    }
}
