using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IStatisticsService
    {
        Task<TokenUsageSummaryDto> GetTokenUsageSummaryAsync(CancellationToken cancellationToken = default);
        Task<List<DailyTokenUsageDto>> GetDailyTokenUsageAsync(int days = 7, CancellationToken cancellationToken = default);
        Task<List<TopDocumentUsageDto>> GetTopDocumentsUsageAsync(int limit = 5, CancellationToken cancellationToken = default);
        Task<List<UserTokenUsageLeaderboardDto>> GetUserLeaderboardAsync(int limit = 10, CancellationToken cancellationToken = default);
    }
}
