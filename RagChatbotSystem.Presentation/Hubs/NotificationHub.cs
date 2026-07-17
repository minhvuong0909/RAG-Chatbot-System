using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public const string AdminGroupName = "role:Admin";
        public const string TeacherGroupName = "role:Teacher";
        public const string StudentGroupName = "role:Student";

        private readonly IDatasetService _datasetService;
        private readonly IChatSessionService _chatSessionService;

        public NotificationHub(IDatasetService datasetService, IChatSessionService chatSessionService)
        {
            _datasetService = datasetService;
            _chatSessionService = chatSessionService;
        }

        public override async Task OnConnectedAsync()
        {
            var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = Context.User?.FindFirstValue(ClaimTypes.Role);
            var hasUserId = Guid.TryParse(userIdValue, out var userId);
            if (hasUserId)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }

            var isAdmin = Context.User?.IsInRole("Admin") == true;
            if (isAdmin)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroupName);
            }
            else if (Context.User?.IsInRole("Teacher") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, TeacherGroupName);
            }
            else if (Context.User?.IsInRole("Student") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, StudentGroupName);
            }

            // Keep authorized teachers/students subscribed even when they are viewing another page.
            // Admins already receive every document event through the admin group.
            if (hasUserId && !isAdmin && !string.IsNullOrWhiteSpace(role))
            {
                var allowedDatasetIds = await _datasetService.GetAccessibleDatasetIdsAsync(userId, role, Context.ConnectionAborted);
                foreach (var datasetId in allowedDatasetIds)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, DatasetGroupName(datasetId));
                }
            }

            await base.OnConnectedAsync();
        }

        public async Task RegisterUser(string userId)
        {
            var currentUserIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(currentUserIdValue, out var currentUserId)
                || !Guid.TryParse(userId, out var requestedUserId)
                || currentUserId != requestedUserId)
            {
                throw new HubException("You cannot register another user's notification group.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{currentUserId}");
        }

        public async Task JoinDatasetGroup(Guid datasetId)
        {
            // Admins already receive every document/dataset event through the admin group.
            // Joining the dataset group as well would deliver the same toast twice.
            if (Context.User?.IsInRole("Admin") == true)
            {
                return;
            }

            var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = Context.User?.FindFirstValue(ClaimTypes.Role);
            if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(role))
            {
                throw new HubException("The current session is invalid.");
            }

            var allowedDatasets = await _datasetService.GetDatasetsForUserAsync(userId, role, Context.ConnectionAborted);
            if (!allowedDatasets.Any(dataset => dataset.DatasetId == datasetId))
            {
                throw new HubException("You do not have access to this subject.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, DatasetGroupName(datasetId));
        }

        public Task LeaveDatasetGroup(Guid datasetId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, DatasetGroupName(datasetId));
        }

        public async Task JoinChatSessionGroup(Guid sessionId)
        {
            var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                throw new HubException("The current session is invalid.");
            }

            var session = await _chatSessionService.GetSessionAsync(sessionId, Context.ConnectionAborted);
            if (session == null)
            {
                throw new HubException("Chat session was not found.");
            }

            if (Context.User?.IsInRole("Admin") != true && session.UserId != userId)
            {
                throw new HubException("You do not have access to this chat session.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, ChatSessionGroupName(sessionId));
        }

        public Task LeaveChatSessionGroup(Guid sessionId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, ChatSessionGroupName(sessionId));
        }

        public static string DatasetGroupName(Guid datasetId)
        {
            return $"dataset:{datasetId}";
        }

        public static string ChatSessionGroupName(Guid sessionId)
        {
            return $"chat-session:{sessionId}";
        }
    }
}
