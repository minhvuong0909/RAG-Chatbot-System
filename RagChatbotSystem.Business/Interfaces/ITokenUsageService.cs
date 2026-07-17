using System;
using System.Threading;
using System.Threading.Tasks;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface ITokenUsageService
    {
        Task<bool> IsLimitExceededAsync(Guid userId, CancellationToken cancellationToken = default);
        Task RecordUsageAsync(Guid userId, Guid datasetId, int tokens, CancellationToken cancellationToken = default);
        Task<int> GetDailyUsageAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
