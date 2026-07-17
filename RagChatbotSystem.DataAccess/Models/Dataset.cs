using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    public class Dataset
    {
        public Guid DatasetId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsPublic { get; set; } = false;
        public bool IsApproved { get; set; } = false;
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public Guid? ArchivedBy { get; set; }

        // Navigation properties
        public virtual User Creator { get; set; } = null!;
        public virtual ICollection<DatasetPermission> DatasetPermissions { get; set; } = new List<DatasetPermission>();
        public virtual TeacherSubjectAssignment? TeacherSubjectAssignment { get; set; }
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
        public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
        public virtual ICollection<Chunk> Chunks { get; set; } = new List<Chunk>();
        public virtual ICollection<VectorRecord> VectorRecords { get; set; } = new List<VectorRecord>();
    }
}
