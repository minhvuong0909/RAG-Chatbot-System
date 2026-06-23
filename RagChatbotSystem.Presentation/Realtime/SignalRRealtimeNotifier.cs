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

            await _hubContext.Clients.Group(NotificationHub.AdminGroupName)
                .SendAsync("DatasetChanged", payload, cancellationToken);

            if (dataset != null)
            {
                await _hubContext.Clients.Group(NotificationHub.DatasetGroupName(dataset.DatasetId))
                    .SendAsync("DatasetChanged", payload, cancellationToken);
            }
        }

        public async Task DatasetAccessChangedAsync(
            Guid userId,
            string action,
            DatasetDto dataset,
            CancellationToken cancellationToken = default)
        {
            var payload = CreateDatasetPayload(action, dataset);
            var userClient = _hubContext.Clients.User(userId.ToString());

            await userClient.SendAsync("DatasetAccessChanged", payload, cancellationToken);
            await userClient.SendAsync("NotificationReceived", new
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

        public async Task UserApprovalChangedAsync(
            Guid userId,
            bool approved,
            CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("NotificationReceived", new
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
