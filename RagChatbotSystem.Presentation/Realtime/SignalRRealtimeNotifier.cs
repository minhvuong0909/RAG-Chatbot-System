using Microsoft.AspNetCore.SignalR;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Presentation.Hubs;

namespace RagChatbotSystem.Presentation.Realtime
{
    public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public SignalRRealtimeNotifier(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task DatasetChangedAsync(
            string action,
            DatasetDto? dataset,
            CancellationToken cancellationToken = default)
        {
            var payload = CreateDatasetPayload(action, dataset);

            // Always notify Admin
            await _hubContext.Clients.Group(NotificationHub.AdminGroupName)
                .SendAsync("DatasetChanged", payload, cancellationToken);

            if (dataset != null)
            {
                // Notify users already subscribed to this dataset group (for update/delete)
                await _hubContext.Clients.Group(NotificationHub.DatasetGroupName(dataset.DatasetId))
                    .SendAsync("DatasetChanged", payload, cancellationToken);

                // For new/approved datasets: notify all teachers so their sidebar updates
                // For unapproved/deleted: also notify teachers so they can remove from view
                if (action is "created" or "approved" or "unapproved" or "updated" or "deleted")
                {
                    await _hubContext.Clients.Group(NotificationHub.TeacherGroupName)
                        .SendAsync("DatasetChanged", payload, cancellationToken);

                    // For public datasets also notify students
                    if (dataset.IsPublic == true)
                    {
                        await _hubContext.Clients.Group(NotificationHub.StudentGroupName)
                            .SendAsync("DatasetChanged", payload, cancellationToken);
                    }
                }
            }
        }

        public async Task DatasetAccessChangedAsync(
            Guid userId,
            string action,
            DatasetDto dataset,
            CancellationToken cancellationToken = default)
        {
            var payload = CreateDatasetPayload(action, dataset);
            // Use group "user_{id}" — reliable with cookie auth, no custom IUserIdProvider needed
            var userGroup = _hubContext.Clients.Group($"user_{userId}");

            await userGroup.SendAsync("DatasetAccessChanged", payload, cancellationToken);
            await userGroup.SendAsync("ReceiveNotification", new
            {
                type = "dataset-access",
                title = "Subject access updated",
                message = BuildAccessMessage(action, dataset.Name),
                changedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            await AdminChangedAsync(
                action,
                $"Access to subject '{dataset.Name}' was {action}.",
                cancellationToken);
        }

        public async Task DocumentProgressAsync(
            Guid datasetId,
            DocumentDto document,
            string action,
            int percentComplete,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                action,
                datasetId,
                documentId = document.DocumentId,
                fileName = document.FileName,
                fileType = document.FileType,
                fileSize = document.FileSize,
                status = document.Status,
                percentComplete,
                changedAt = DateTimeOffset.UtcNow
            };

            await _hubContext.Clients.Group(NotificationHub.DatasetGroupName(datasetId))
                .SendAsync("DocumentProgress", payload, cancellationToken);

            await _hubContext.Clients.Group(NotificationHub.AdminGroupName)
                .SendAsync("DocumentProgress", payload, cancellationToken);
        }

        public Task ChatSessionChangedAsync(
            Guid userId,
            Guid datasetId,
            ChatSessionDto session,
            string action,
            CancellationToken cancellationToken = default)
        {
            return _hubContext.Clients.Group($"user_{userId}").SendAsync("ChatSessionChanged", new
            {
                action,
                datasetId,
                sessionId = session.SessionId,
                title = session.Title,
                updatedAt = session.UpdatedAt,
                changedAt = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        public Task ChatMessageSavedAsync(
            Guid userId,
            Guid datasetId,
            Guid sessionId,
            SendChatMessageResponse response,
            CancellationToken cancellationToken = default)
        {
            return _hubContext.Clients.Group(NotificationHub.ChatSessionGroupName(sessionId))
                .SendAsync("ChatMessageSaved", new
                {
                    userId,
                    datasetId,
                    sessionId,
                    userMessage = CreateChatMessagePayload(response.UserMessage),
                    assistantMessage = CreateChatMessagePayload(response.AssistantMessage),
                    citations = response.Citations.Select(c => new
                    {
                        citationId = c.CitationId,
                        messageId = c.MessageId,
                        chunkId = c.ChunkId,
                        documentId = c.DocumentId,
                        fileName = c.FileName,
                        pageNumber = c.PageNumber,
                        quoteText = c.QuoteText,
                        sourceLabel = c.SourceLabel
                    }),
                    changedAt = DateTimeOffset.UtcNow
                }, cancellationToken);
        }

        public async Task UserApprovalChangedAsync(
            Guid userId,
            bool approved,
            CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", new
            {
                type = "account",
                title = "Account status updated",
                message = approved ? "Your account was approved." : "Your account was deactivated.",
                changedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            await AdminChangedAsync(
                approved ? "user-approved" : "user-deactivated",
                approved ? "A teacher account was approved." : "A teacher account was deactivated.",
                cancellationToken);
        }

        public Task AdminChangedAsync(
            string action,
            string message,
            CancellationToken cancellationToken = default)
        {
            return _hubContext.Clients.Group(NotificationHub.AdminGroupName).SendAsync("AdminChanged", new
            {
                action,
                message,
                changedAt = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        private static object CreateDatasetPayload(string action, DatasetDto? dataset)
        {
            return new
            {
                action,
                datasetId = dataset?.DatasetId,
                name = dataset?.Name ?? "Unknown subject",
                description = dataset?.Description,
                documentCount = dataset?.DocumentCount ?? 0,
                isPublic = dataset?.IsPublic,
                isApproved = dataset?.IsApproved,
                assignedTeacherId = dataset?.AssignedTeacherId,
                assignedTeacherName = dataset?.AssignedTeacherName,
                changedAt = DateTimeOffset.UtcNow
            };
        }

        private static object CreateChatMessagePayload(ChatMessageDto message)
        {
            return new
            {
                messageId = message.MessageId,
                sessionId = message.SessionId,
                role = message.Role,
                content = message.Content,
                createdAt = message.CreatedAt
            };
        }

        private static string BuildAccessMessage(string action, string datasetName)
        {
            return action switch
            {
                "assigned" => $"You were assigned to subject '{datasetName}'.",
                "unassigned" => $"You were unassigned from subject '{datasetName}'.",
                "granted" => $"You were granted access to subject '{datasetName}'.",
                "revoked" => $"Your access to subject '{datasetName}' was revoked.",
                _ => $"Your access to subject '{datasetName}' changed."
            };
        }
    }
}
