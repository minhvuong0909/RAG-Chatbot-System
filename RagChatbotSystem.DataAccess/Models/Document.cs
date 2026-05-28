using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    public class Document
    {
        public Guid DocumentId { get; set; }
        public Guid DatasetId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Dataset Dataset { get; set; } = null!;
        public virtual User Uploader { get; set; } = null!;
        public virtual ICollection<Chunk> Chunks { get; set; } = new List<Chunk>();
        public virtual ICollection<VectorRecord> VectorRecords { get; set; } = new List<VectorRecord>();
        public virtual ICollection<Citation> Citations { get; set; } = new List<Citation>();
    }
}
