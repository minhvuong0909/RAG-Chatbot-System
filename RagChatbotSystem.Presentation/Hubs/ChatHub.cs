using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatSessionService _chatSessionService;

        public ChatHub(IChatSessionService chatSessionService)
        {
            _chatSessionService = chatSessionService;
        }

        public async Task JoinSession(string sessionId)
        {
            if (Guid.TryParse(sessionId, out var sessionGuid))
            {
                var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                var session = await _chatSessionService.GetSessionAsync(sessionGuid, Context.ConnectionAborted);
                if (!Guid.TryParse(userIdValue, out var userId) || session == null ||
                    (Context.User?.IsInRole("Admin") != true && session.UserId != userId))
                {
                    throw new HubException("Bạn không có quyền truy cập cuộc trò chuyện này.");
                }
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
