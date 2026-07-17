using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    public class EvaluationRun
    {
        public Guid Id { get; set; }
        public Guid DatasetId { get; set; }
        public Guid BenchmarkDefinitionId { get; set; }
        public Guid EvaluationProfileId { get; set; }
        public Guid RunByUserId { get; set; }
        public string ProviderKey { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string PromptVersion { get; set; } = "rag-v1";
        public string Status { get; set; } = "Pending";
        public int TotalQuestions { get; set; }
        public int CompletedQuestions { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public virtual Dataset Dataset { get; set; } = null!;
        public virtual BenchmarkDefinition BenchmarkDefinition { get; set; } = null!;
        public virtual EvaluationProfile EvaluationProfile { get; set; } = null!;
        public virtual User RunByUser { get; set; } = null!;
        public virtual ICollection<EvaluationResult> Results { get; set; } = new List<EvaluationResult>();
    }
}
