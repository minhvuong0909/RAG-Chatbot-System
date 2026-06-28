using System;

namespace RagChatbotSystem.DataAccess.Models
{
    public class SystemSetting
    {
        public int Id { get; set; }
        public int ChunkSize { get; set; } = 500;
        public int ChunkOverlap { get; set; } = 100;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
