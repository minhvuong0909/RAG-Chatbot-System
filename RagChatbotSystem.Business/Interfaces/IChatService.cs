using System;
using System.Threading.Tasks;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IChatService
    {
        Task<ChatMessage> SendChatMessageAsync(Guid sessionId, string userQuestion);
    }
}
