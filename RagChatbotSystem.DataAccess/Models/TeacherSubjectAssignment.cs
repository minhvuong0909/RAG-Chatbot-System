using System;

namespace RagChatbotSystem.DataAccess.Models
{
    public class TeacherSubjectAssignment
    {
        public Guid AssignmentId { get; set; }
        public Guid TeacherId { get; set; }
        public Guid DatasetId { get; set; }
        public Guid AssignedBy { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public virtual User Teacher { get; set; } = null!;
        public virtual User AssignedByAdmin { get; set; } = null!;
        public virtual Dataset Dataset { get; set; } = null!;
    }
}
