using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Hubs;

namespace RagChatbotSystem.Presentation.Services
{
    public class DocumentProgressNotifier : IDocumentProgressNotifier
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public DocumentProgressNotifier(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyProgressAsync(
            Guid datasetId,
            Guid documentId,
            string status,
            int percentComplete,
            CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.Group(NotificationHub.DatasetGroupName(datasetId))
                .SendAsync("DocumentProgress", new
                {
                    documentId,
                    status,
                    percentComplete,
                    changedAt = DateTimeOffset.UtcNow
                }, cancellationToken);
        }
    }
}
