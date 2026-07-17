using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    /// <summary>
    /// A versioned, immutable set of questions used to evaluate one dataset.
    /// Once a run has started, its version must not be edited.
    /// </summary>
    public class BenchmarkDefinition
    {
        public Guid Id { get; set; }
        public Guid DatasetId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "v1";
        public string? Description { get; set; }
        public bool IsLocked { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Dataset Dataset { get; set; } = null!;
        public virtual User CreatedByUser { get; set; } = null!;
        public virtual ICollection<BenchmarkQuestion> Questions { get; set; } = new List<BenchmarkQuestion>();
        public virtual ICollection<EvaluationRun> Runs { get; set; } = new List<EvaluationRun>();
    }
}
