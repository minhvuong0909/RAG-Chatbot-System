using System.Threading;
using System.Threading.Tasks;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface ISystemSettingService
    {
        Task<int> GetChunkSizeAsync(CancellationToken cancellationToken = default);
        Task<int> GetChunkOverlapAsync(CancellationToken cancellationToken = default);
        Task<int> GetDailyTokenLimitAsync(CancellationToken cancellationToken = default);
        Task UpdateSettingsAsync(int chunkSize, int chunkOverlap, int dailyTokenLimit, CancellationToken cancellationToken = default);
    }
}
