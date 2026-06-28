using System.Threading;
using System.Threading.Tasks;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface ISystemSettingService
    {
        Task<int> GetChunkSizeAsync(CancellationToken cancellationToken = default);
        Task<int> GetChunkOverlapAsync(CancellationToken cancellationToken = default);
        Task UpdateSettingsAsync(int chunkSize, int chunkOverlap, CancellationToken cancellationToken = default);
    }
}
