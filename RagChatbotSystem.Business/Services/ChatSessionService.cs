using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Repositories;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Services
{
    public class ChatSessionService : IChatSessionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<ChatSession> _sessionRepository;
        private readonly IGenericRepository<User> _userRepository;
        private readonly IGenericRepository<Dataset> _datasetRepository;
        private readonly IGenericRepository<ChatMessage> _messageRepository;
        private readonly IGenericRepository<Citation> _citationRepository;

        public ChatSessionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _sessionRepository = _unitOfWork.Repository<ChatSession>();
            _userRepository = _unitOfWork.Repository<User>();
            _datasetRepository = _unitOfWork.Repository<Dataset>();
            _messageRepository = _unitOfWork.Repository<ChatMessage>();
            _citationRepository = _unitOfWork.Repository<Citation>();
        }

        public async Task<IReadOnlyList<ChatSessionDto>> GetSessionsAsync(Guid? userId = null, Guid? datasetId = null, CancellationToken cancellationToken = default)
        {
            var query = _sessionRepository.GetQueryable().AsNoTracking();

            if (userId.HasValue)
            {
                query = query.Where(s => s.UserId == userId.Value);
            }

            if (datasetId.HasValue)
            {
                query = query.Where(s => s.DatasetId == datasetId.Value);
            }

            return await query
                .OrderByDescending(s => s.UpdatedAt)
                .Select(s => new ChatSessionDto(
                    s.SessionId,
                    s.UserId,
                    s.DatasetId,
                    s.Title,
                    s.StartedAt,
                    s.UpdatedAt))
                .ToListAsync(cancellationToken);
        }

        public async Task<ChatSessionDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            return await _sessionRepository.GetQueryable()
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
            var userExists = await _userRepository.GetQueryable().AnyAsync(u => u.UserId == request.UserId, cancellationToken);
            if (!userExists)
            {
                throw new InvalidOperationException("User was not found.");
            }

            var datasetExists = await _datasetRepository.GetQueryable().AnyAsync(d => d.DatasetId == request.DatasetId, cancellationToken);
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

            await _sessionRepository.AddAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ToDto(session);
        }

        public async Task<IReadOnlyList<ChatMessageDto>> GetMessageHistoryAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            var sessionExists = await _sessionRepository.GetQueryable().AnyAsync(s => s.SessionId == sessionId, cancellationToken);
            if (!sessionExists)
            {
                throw new KeyNotFoundException("Chat session was not found.");
            }

            return await _messageRepository.GetQueryable()
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

        public async Task<IReadOnlyList<CitationDto>> GetCitationsAsync(Guid messageId, CancellationToken cancellationToken = default)
        {
            var messageExists = await _messageRepository.GetQueryable().AnyAsync(m => m.MessageId == messageId, cancellationToken);
            if (!messageExists)
            {
                throw new KeyNotFoundException("Chat message was not found.");
            }

            return await _citationRepository.GetQueryable()
                .AsNoTracking()
                .Include(c => c.Document)
                .Where(c => c.MessageId == messageId)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new CitationDto(
                    c.CitationId,
                    c.MessageId,
                    c.ChunkId,
                    c.DocumentId,
                    c.Document.FileName,
                    c.PageNumber,
                    c.QuoteText,
                    c.SourceLabel,
                    c.CreatedAt))
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
