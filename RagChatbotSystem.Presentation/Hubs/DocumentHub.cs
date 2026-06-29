using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace RagChatbotSystem.Presentation.Hubs
{
    public class DocumentHub : Hub
    {
        public async Task JoinDataset(string datasetId)
        {
            if (Guid.TryParse(datasetId, out var datasetGuid))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"dataset_{datasetGuid}");
            }
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
