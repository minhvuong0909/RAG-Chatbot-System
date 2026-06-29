using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace RagChatbotSystem.Presentation.Hubs
{
    public class ChatHub : Hub
    {
        public async Task JoinSession(string sessionId)
        {
            if (Guid.TryParse(sessionId, out var sessionGuid))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"session_{sessionGuid}");
            }
        }

        public async Task LeaveSession(string sessionId)
        {
            if (Guid.TryParse(sessionId, out var sessionGuid))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session_{sessionGuid}");
            }
        }
    }
}
