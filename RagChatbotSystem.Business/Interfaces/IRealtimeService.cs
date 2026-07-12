using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IRealtimeService
    {
        Task SendChatChunkAsync(Guid sessionId, Guid messageId, string chunk, CancellationToken cancellationToken = default);
        Task SendChatCompleteAsync(
            Guid sessionId,
            ChatMessageDto assistantMessage,
            IReadOnlyList<CitationDto> citations,
            CreditSpendResultDto? creditSpend = null,
            CreditBalanceDto? creditBalance = null,
            CancellationToken cancellationToken = default);
        Task SendChatFailedAsync(Guid sessionId, Guid messageId, string errorMessage, CancellationToken cancellationToken = default);
        Task SendCreditBalanceChangedAsync(Guid userId, CreditBalanceDto balance, string reason, CreditSpendResultDto? creditSpend = null, CancellationToken cancellationToken = default);
        Task SendDocumentProgressAsync(Guid datasetId, Guid documentId, string status, int progressPercentage, CancellationToken cancellationToken = default);
        Task SendNotificationAsync(Guid userId, string message, CancellationToken cancellationToken = default);
        Task TriggerUiUpdateAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
    }
}
