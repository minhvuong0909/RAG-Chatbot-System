using System;

namespace RagChatbotSystem.Business.DTOs
{
    public class TokenUsageSummaryDto
    {
        public int TotalTokensUsed { get; set; }
        public int TotalQueriesCount { get; set; }
        public int TodayTokensUsed { get; set; }
        public int ActiveUsersCount { get; set; }
    }

    public class DailyTokenUsageDto
    {
        public DateTime Date { get; set; }
        public string FormattedDate { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public int QueryCount { get; set; }
    }

    public class TopDocumentUsageDto
    {
        public Guid DocumentId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string DatasetName { get; set; } = string.Empty;
        public int CitationCount { get; set; }
    }

    public class UserTokenUsageLeaderboardDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int TotalTokensUsed { get; set; }
        public int TotalQueriesCount { get; set; }
    }
}
