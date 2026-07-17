using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    /// <summary>One reproducible retrieval experiment configuration.</summary>
    public class EvaluationProfile
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string ChunkingStrategy { get; set; } = "fixed";
        public int ChunkSize { get; set; }
        public int ChunkOverlap { get; set; }
        public string EmbeddingModel { get; set; } = string.Empty;
        public int TopK { get; set; } = 10;
        public double SemanticWeight { get; set; } = 0.7;
        public double LexicalWeight { get; set; } = 0.3;
        public bool EnableRerank { get; set; } = true;
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<EvaluationRun> Runs { get; set; } = new List<EvaluationRun>();
    }
}
