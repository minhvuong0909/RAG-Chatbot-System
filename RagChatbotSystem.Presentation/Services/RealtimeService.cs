using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Hubs;

namespace RagChatbotSystem.Presentation.Services
{
    public class RealtimeService : IRealtimeService
    {
        private readonly IHubContext<ChatHub> _chatHubContext;
        private readonly IHubContext<DocumentHub> _documentHubContext;
        private readonly IHubContext<NotificationHub> _notificationHubContext;

        public RealtimeService(
            IHubContext<ChatHub> chatHubContext,
            IHubContext<DocumentHub> documentHubContext,
            IHubContext<NotificationHub> notificationHubContext)
        {
            _chatHubContext = chatHubContext;
            _documentHubContext = documentHubContext;
            _notificationHubContext = notificationHubContext;
        }

        public async Task SendChatChunkAsync(Guid sessionId, Guid messageId, string chunk, CancellationToken cancellationToken = default)
        {
            await _chatHubContext.Clients.Group($"session_{sessionId}")
                .SendAsync("ReceiveChatChunk", messageId, chunk, cancellationToken);
        }

        public async Task SendChatCompleteAsync(
            Guid sessionId,
            ChatMessageDto message,
            IReadOnlyList<CitationDto> citations,
            CreditSpendResultDto? creditSpend = null,
            CreditBalanceDto? creditBalance = null,
            CancellationToken cancellationToken = default)
        {
            await _chatHubContext.Clients.Group($"session_{sessionId}")
                .SendAsync("ReceiveChatComplete", message, citations, new
                {
                    creditSpend,
                    creditBalance
                }, cancellationToken);
        }

        public async Task SendChatFailedAsync(Guid sessionId, Guid messageId, string errorMessage, CancellationToken cancellationToken = default)
        {
            await _chatHubContext.Clients.Group($"session_{sessionId}")
                .SendAsync("ReceiveChatFailed", messageId, errorMessage, cancellationToken);
        }

        public Task SendCreditBalanceChangedAsync(
            Guid userId,
            CreditBalanceDto balance,
            string reason,
            CreditSpendResultDto? creditSpend = null,
            CancellationToken cancellationToken = default)
        {
            return _notificationHubContext.Clients.Group($"user_{userId}")
                .SendAsync("CreditBalanceChanged", new
                {
                    reason,
                    balance,
                    creditSpend,
                    changedAt = DateTimeOffset.UtcNow
                }, cancellationToken);
        }

        public async Task SendDocumentProgressAsync(Guid datasetId, Guid documentId, string status, int percentComplete, CancellationToken cancellationToken = default)
        {
            await _documentHubContext.Clients.Group($"dataset_{datasetId}")
                .SendAsync("ReceiveDocumentProgress", documentId, status, percentComplete, cancellationToken);
        }

        public async Task SendNotificationAsync(Guid userId, string message, CancellationToken cancellationToken = default)
        {
            await _notificationHubContext.Clients.Group($"user_{userId}")
                .SendAsync("ReceiveNotification", message, cancellationToken);
        }

        public async Task TriggerUiUpdateAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default)
        {
            // Send to Admin group (always needs to see all updates)
            await _notificationHubContext.Clients.Group(NotificationHub.AdminGroupName)
                .SendAsync("TriggerUiUpdate", entityType, entityId, cancellationToken);

            // Send to the specific dataset/entity group so only authorized members receive it
            var entityGroup = NotificationHub.DatasetGroupName(entityId);
            await _notificationHubContext.Clients.Group(entityGroup)
                .SendAsync("TriggerUiUpdate", entityType, entityId, cancellationToken);
        }
    }
}
