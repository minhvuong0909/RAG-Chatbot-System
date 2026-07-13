using System;

namespace RagChatbotSystem.DataAccess.Models
{
    public class SystemSetting
    {
        public int Id { get; set; }
        public int ChunkSize { get; set; } = 500;
        public int ChunkOverlap { get; set; } = 100;
        public int DailyTokenLimit { get; set; } = 50000;
        public int DailyFreeCredits { get; set; } = 60;
        public int CreditTokenUnit { get; set; } = 1000;
        public int CreditOutputTokenWeight { get; set; } = 4;
        public bool EnableCreditSystem { get; set; } = true;
        public int ExamSeasonDailyFreeCredits { get; set; } = 100;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
