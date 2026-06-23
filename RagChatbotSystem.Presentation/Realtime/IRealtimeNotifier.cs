using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Presentation.Realtime
{
    public interface IRealtimeNotifier
    {
        Task DatasetChangedAsync(string action, DatasetDto? dataset, CancellationToken cancellationToken = default);
        Task DatasetAccessChangedAsync(Guid userId, string action, DatasetDto dataset, CancellationToken cancellationToken = default);
        Task UserApprovalChangedAsync(Guid userId, bool approved, CancellationToken cancellationToken = default);
        Task AdminChangedAsync(string action, string message, CancellationToken cancellationToken = default);
    }
}
