using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Services
{
    public class ChatSessionService : IChatSessionService
    {
        private readonly AppDbContext _context;

        public ChatSessionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ChatSessionDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            return await _context.ChatSessions
                .AsNoTracking()
                .Where(s => s.SessionId == sessionId)
                .Select(s => new ChatSessionDto(
                    s.SessionId,
                    s.UserId,
                    s.DatasetId,
                    s.Title,
                    s.StartedAt,
                    s.UpdatedAt))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<ChatSessionDto> CreateSessionAsync(CreateChatSessionRequest request, CancellationToken cancellationToken = default)
        {
            var userExists = await _context.Users.AnyAsync(u => u.UserId == request.UserId, cancellationToken);
            if (!userExists)
            {
                throw new InvalidOperationException("User was not found.");
            }

            var datasetExists = await _context.Datasets.AnyAsync(d => d.DatasetId == request.DatasetId, cancellationToken);
            if (!datasetExists)
            {
                throw new InvalidOperationException("Dataset was not found.");
            }

            var now = DateTime.UtcNow;
            var session = new ChatSession
            {
                SessionId = Guid.NewGuid(),
                UserId = request.UserId,
                DatasetId = request.DatasetId,
                Title = string.IsNullOrWhiteSpace(request.Title) ? "New chat" : request.Title.Trim(),
                StartedAt = now,
                UpdatedAt = now
            };

            _context.ChatSessions.Add(session);
            await _context.SaveChangesAsync(cancellationToken);

            return ToDto(session);
        }

        public async Task<IReadOnlyList<ChatMessageDto>> GetMessageHistoryAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            var sessionExists = await _context.ChatSessions.AnyAsync(s => s.SessionId == sessionId, cancellationToken);
            if (!sessionExists)
            {
                throw new KeyNotFoundException("Chat session was not found.");
            }

            return await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatMessageDto(
                    m.MessageId,
                    m.SessionId,
                    m.Role,
                    m.Content,
                    m.CreatedAt))
                .ToListAsync(cancellationToken);
        }

        private static ChatSessionDto ToDto(ChatSession session)
        {
            return new ChatSessionDto(
                session.SessionId,
                session.UserId,
                session.DatasetId,
                session.Title,
                session.StartedAt,
                session.UpdatedAt);
        }

        private static ChatMessageDto ToDto(ChatMessage message)
        {
            return new ChatMessageDto(
                message.MessageId,
                message.SessionId,
                message.Role,
                message.Content,
                message.CreatedAt);
        }
    }
}
