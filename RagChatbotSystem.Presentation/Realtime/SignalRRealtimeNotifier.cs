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

                // For unapproved/deleted: also notify the assigned teacher so they can remove it from view
                if (action is "created" or "approved" or "unapproved" or "updated" or "archived" or "restored" or "deleted")
                {
                    if (dataset.AssignedTeacherId.HasValue)
                    {
                        var teacherGroup = _hubContext.Clients.Group($"user_{dataset.AssignedTeacherId.Value}");
                        await teacherGroup.SendAsync("DatasetChanged", payload, cancellationToken);
                        if (action is "updated" or "archived" or "restored" or "deleted")
                        {
                            await teacherGroup.SendAsync("ReceiveNotification", new
                            {
                                type = "dataset",
                                title = "Môn học đã thay đổi",
                                message = action switch
                                {
                                    "updated" => $"Môn học '{dataset.Name}' vừa được quản trị viên cập nhật.",
                                    "archived" => $"Môn học '{dataset.Name}' vừa được lưu trữ. Lịch sử vẫn được giữ ở chế độ chỉ đọc.",
                                    "restored" => $"Môn học '{dataset.Name}' vừa được khôi phục.",
                                    _ => $"Môn học '{dataset.Name}' vừa được quản trị viên xóa."
                                },
                                changedAt = DateTimeOffset.UtcNow
                            }, cancellationToken);
                        }
                    }

                    // For public datasets also notify students, but only if they are approved
                    if (dataset.IsPublic == true && dataset.IsApproved == true)
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
                title = "Quyền truy cập môn học đã thay đổi",
                message = BuildAccessMessage(action, dataset.Name),
                changedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            await AdminChangedAsync(
                action,
                $"Quyền truy cập môn học '{dataset.Name}' vừa được cập nhật.",
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
                        sourceLabel = c.SourceLabel,
                        fileType = c.FileType,
                        chunkIndex = c.ChunkIndex
                    }),
                    changedAt = DateTimeOffset.UtcNow
                }, cancellationToken);
        }

        public Task CreditBalanceChangedAsync(
            Guid userId,
            CreditBalanceDto balance,
            string reason,
            CancellationToken cancellationToken = default)
        {
            return _hubContext.Clients.Group($"user_{userId}")
                .SendAsync("CreditBalanceChanged", new
                {
                    reason,
                    balance,
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
                title = "Trạng thái tài khoản đã thay đổi",
                message = approved ? "Tài khoản của bạn đã được kích hoạt." : "Tài khoản của bạn đã bị khóa.",
                changedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            await AdminChangedAsync(
                approved ? "user-approved" : "user-deactivated",
                approved ? "Một tài khoản giảng viên đã được kích hoạt." : "Một tài khoản giảng viên đã bị khóa.",
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
                name = dataset?.Name ?? "Môn học chưa xác định",
                description = dataset?.Description,
                documentCount = dataset?.DocumentCount ?? 0,
                isPublic = dataset?.IsPublic,
                isApproved = dataset?.IsApproved,
                assignedTeacherId = dataset?.AssignedTeacherId,
                assignedTeacherName = dataset?.AssignedTeacherName,
                isArchived = dataset?.IsArchived,
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
                "assigned" => $"Bạn vừa được phân công môn học '{datasetName}'.",
                "unassigned" => $"Bạn không còn phụ trách môn học '{datasetName}'.",
                "granted" => $"Bạn vừa được cấp quyền truy cập môn học '{datasetName}'.",
                "revoked" => $"Quyền truy cập môn học '{datasetName}' của bạn đã bị thu hồi.",
                _ => $"Quyền truy cập môn học '{datasetName}' của bạn vừa thay đổi."
            };
        }
    }
}
