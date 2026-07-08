using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IChatSessionService
    {
        Task<IReadOnlyList<ChatSessionDto>> GetSessionsAsync(Guid? userId = null, Guid? datasetId = null, CancellationToken cancellationToken = default);
        Task<ChatSessionDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
        Task<ChatSessionDto?> GetSessionForMessageAsync(Guid messageId, CancellationToken cancellationToken = default);
        Task<ChatSessionDto> CreateSessionAsync(CreateChatSessionRequest request, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ChatMessageDto>> GetMessageHistoryAsync(Guid sessionId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CitationDto>> GetCitationsAsync(Guid messageId, CancellationToken cancellationToken = default);
    }
}
