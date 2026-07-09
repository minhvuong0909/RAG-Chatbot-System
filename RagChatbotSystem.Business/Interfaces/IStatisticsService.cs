using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IStatisticsService
    {
        Task<TokenUsageSummaryDto> GetTokenUsageSummaryAsync(IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default);
        Task<List<DailyTokenUsageDto>> GetDailyTokenUsageAsync(int days = 7, IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default);
        Task<List<TopDocumentUsageDto>> GetTopDocumentsUsageAsync(int limit = 5, IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default);
        Task<List<TopSubjectUsageDto>> GetTopSubjectsByQuestionCountAsync(int limit = 5, IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default);
        Task<List<TopSubjectUsageDto>> GetSubjectLearningActivityByQuestionCountAsync(int limit = 5, IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default);
        Task<List<UserTokenUsageLeaderboardDto>> GetUserLeaderboardAsync(int limit = 10, IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default);
        Task<int> GetActiveStudentCountAsync(IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default);
        Task<List<StudentLearningEngagementDto>> GetStudentEngagementByQuestionCountAsync(int limit = 10, IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default);
    }
}
