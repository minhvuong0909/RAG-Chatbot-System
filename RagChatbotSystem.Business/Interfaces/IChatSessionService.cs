using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IChatSessionService
    {
        Task<ChatSessionDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
        Task<ChatSessionDto> CreateSessionAsync(CreateChatSessionRequest request, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ChatMessageDto>> GetMessageHistoryAsync(Guid sessionId, CancellationToken cancellationToken = default);
    }
}
