using System;
using System.Collections.Generic;

namespace RagChatbotSystem.DataAccess.Models
{
    public class User
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsApproved { get; set; } = true;
        public bool MustChangePassword { get; set; } = false;
        public DateTime? TemporaryPasswordExpiresAt { get; set; }
        public DateTime? LastPasswordChangedAt { get; set; }
        public Guid? CreatedByAdminId { get; set; }
        public DateTime? LastLoginAt { get; set; }

        // Navigation properties
        public virtual ICollection<DatasetPermission> DatasetPermissions { get; set; } = new List<DatasetPermission>();
        public virtual ICollection<TeacherSubjectAssignment> TeacherSubjectAssignments { get; set; } = new List<TeacherSubjectAssignment>();
        public virtual ICollection<Dataset> Datasets { get; set; } = new List<Dataset>();
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
        public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    }
}

