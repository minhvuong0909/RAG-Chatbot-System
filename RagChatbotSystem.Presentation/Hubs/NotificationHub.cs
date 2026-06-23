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
        private readonly IDatasetService _datasetService;

        public NotificationHub(IDatasetService datasetService)
        {
            _datasetService = datasetService;
        }

        public override async Task OnConnectedAsync()
        {
            var userIdVal = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdVal) && Guid.TryParse(userIdVal, out var userGuid))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userGuid}");
            }

            if (Context.User?.IsInRole("Admin") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroupName);
            }

            await base.OnConnectedAsync();
        }

        public async Task RegisterUser(string userId)
        {
            if (Guid.TryParse(userId, out var userGuid))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userGuid}");
            }
        }

        public async Task JoinDatasetGroup(Guid datasetId)
        {
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

        public static string DatasetGroupName(Guid datasetId)
        {
            return $"dataset:{datasetId}";
        }
    }
}
