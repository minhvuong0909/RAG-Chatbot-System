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
    public class DocumentHub : Hub
    {
        private readonly IDatasetService _datasetService;

        public DocumentHub(IDatasetService datasetService)
        {
            _datasetService = datasetService;
        }

        public async Task JoinDataset(string datasetId)
        {
            var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = Context.User?.FindFirstValue(ClaimTypes.Role);
            if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(role))
            {
                throw new HubException("The current session is invalid.");
            }

            if (!Guid.TryParse(datasetId, out var datasetGuid))
            {
                throw new HubException("The subject identifier is invalid.");
            }

            var allowedDatasets = await _datasetService.GetDatasetsForUserAsync(userId, role, Context.ConnectionAborted);
            if (!allowedDatasets.Any(dataset => dataset.DatasetId == datasetGuid))
            {
                throw new HubException("You do not have access to this subject.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"dataset_{datasetGuid}");
        }

        public async Task LeaveDataset(string datasetId)
        {
            if (Guid.TryParse(datasetId, out var datasetGuid))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dataset_{datasetGuid}");
            }
        }
    }
}
