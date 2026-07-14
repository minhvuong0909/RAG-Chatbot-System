using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Presentation.Realtime
{
    public interface IRealtimeNotifier
    {
        Task DatasetChangedAsync(string action, DatasetDto? dataset, CancellationToken cancellationToken = default);
        Task DatasetAccessChangedAsync(Guid userId, string action, DatasetDto dataset, CancellationToken cancellationToken = default);
        Task DocumentProgressAsync(Guid datasetId, DocumentDto document, string action, int percentComplete, CancellationToken cancellationToken = default);
        Task ChatSessionChangedAsync(Guid userId, Guid datasetId, ChatSessionDto session, string action, CancellationToken cancellationToken = default);
        Task ChatMessageSavedAsync(Guid userId, Guid datasetId, Guid sessionId, SendChatMessageResponse response, CancellationToken cancellationToken = default);
        Task CreditBalanceChangedAsync(Guid userId, CreditBalanceDto balance, string reason, CancellationToken cancellationToken = default);
        Task UserApprovalChangedAsync(Guid userId, bool approved, CancellationToken cancellationToken = default);
        Task AdminChangedAsync(string action, string message, CancellationToken cancellationToken = default);
    }
}
