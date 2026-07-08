using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Business.Services
{
    public class ChatService : IChatService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<ChatSession> _sessionRepository;
        private readonly IGenericRepository<ChatMessage> _messageRepository;
        private readonly IGenericRepository<Citation> _citationRepository;
        private readonly IGenericRepository<Document> _documentRepository;
        private readonly IGenericRepository<Chunk> _chunkRepository;
        private readonly IRagApiClient _ragApiClient;
        private readonly ILlmService _llmService;
        private readonly IRealtimeService _realtimeService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IUnitOfWork unitOfWork,
            IRagApiClient ragApiClient,
            ILlmService llmService,
            IRealtimeService realtimeService,
            ILogger<ChatService> logger)
        {
            _unitOfWork = unitOfWork;
            _sessionRepository = _unitOfWork.Repository<ChatSession>();
            _messageRepository = _unitOfWork.Repository<ChatMessage>();
            _citationRepository = _unitOfWork.Repository<Citation>();
            _documentRepository = _unitOfWork.Repository<Document>();
            _chunkRepository = _unitOfWork.Repository<Chunk>();
            _ragApiClient = ragApiClient;
            _llmService = llmService;
            _realtimeService = realtimeService;
            _logger = logger;
        }

        public async Task<SendChatMessageResponse> SendChatMessageAsync(Guid sessionId, string userQuestion, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                throw new ArgumentException("Question is required.", nameof(userQuestion));
            }

            var session = await _sessionRepository.GetQueryable().FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
            if (session == null)
            {
                throw new ArgumentException("Chat session not found.");
            }

            var hasCompletedDocuments = await _documentRepository.GetQueryable()
                .AsNoTracking()
                .AnyAsync(d => d.DatasetId == session.DatasetId && !d.IsDeleted && d.Status == "Completed", cancellationToken);

            if (!hasCompletedDocuments)
            {
                throw new InvalidOperationException("This subject does not have any indexed documents yet. Please upload a document before starting chat.");
            }

            var now = DateTime.UtcNow;
            var userMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                SessionId = sessionId,
                Role = "User",
                Content = userQuestion.Trim(),
                CreatedAt = now
            };

            await _messageRepository.AddAsync(userMessage, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken); // Save first to ensure User message is persistent

            var retrieveResult = await _ragApiClient.RetrieveAsync(new RetrieveRequestDto
            {
                Query = userQuestion,
                TopK = 10,
                SemanticWeight = 0.7,
                LexicalWeight = 0.3,
                EnableRerank = true
            });

            var datasetIdStr = session.DatasetId.ToString();
            
            _logger.LogInformation("Retrieve returned {Count} documents from RAG API for query '{Query}'. Filter DatasetId: '{DatasetId}'",
                retrieveResult.Documents?.Count ?? 0, userQuestion, datasetIdStr);

            if (retrieveResult.Documents != null)
            {
                for (int i = 0; i < retrieveResult.Documents.Count; i++)
                {
                    var doc = retrieveResult.Documents[i];
                    var hasDsId = doc.Metadata.TryGetValue("dataset_id", out var dsIdVal);
                    _logger.LogInformation("Candidate {Index}: Content length={Length}, has dataset_id={HasDsId}, dataset_id value='{DsIdVal}'",
                        i, doc.PageContent?.Length ?? 0, hasDsId, dsIdVal);
                }
            }

            var contextDocs = (retrieveResult.Documents ?? Enumerable.Empty<DocumentModelDto>())
                .Where(doc => doc.Metadata.TryGetValue("dataset_id", out var dsId)
                    && string.Equals(dsId?.ToString(), datasetIdStr, StringComparison.OrdinalIgnoreCase))
                .ToList();

            contextDocs = await FilterActiveCompletedContextAsync(contextDocs, session.DatasetId, cancellationToken);

            var contextText = contextDocs.Count > 0
                ? string.Join("\n\n---\n\n", contextDocs.Select(d => d.PageContent))
                : "Khong tim thay tai lieu phu hop trong ngu canh.";

            var prompt =
                "Bạn là một trợ lý AI hữu ích. Hãy trả lời câu hỏi của người dùng bằng tiếng Việt dựa vào phần Ngữ cảnh được cung cấp dưới đây.\n" +
                "Nếu thông tin không có trong Ngữ cảnh, hãy trả lời là \"Tôi không tìm thấy thông tin này trong tài liệu của bạn.\" và khuyên người dùng bổ sung tài liệu. Không tự bịa ra câu trả lời nằm ngoài tài liệu.\n\n" +
                $"Ngữ cảnh:\n{contextText}\n\n" +
                $"Câu hỏi: {userQuestion}\n" +
                "Câu trả lời:";

            var assistantMessageId = Guid.NewGuid();
            var accumulatedText = new StringBuilder();

            // Try to stream the response first
            try
            {
                await foreach (var chunk in _llmService.GenerateAnswerStreamAsync(prompt).WithCancellation(cancellationToken))
                {
                    accumulatedText.Append(chunk);
                    // Push each chunk in real-time via SignalR
                    await _realtimeService.SendChatChunkAsync(sessionId, assistantMessageId, chunk, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Streaming failed. Falling back to synchronous generation.");
                // If stream fails, fallback to generate answer synchronously
                var fallbackAnswer = await _llmService.GenerateAnswerAsync(prompt);
                accumulatedText.Clear();
                accumulatedText.Append(fallbackAnswer);
                await _realtimeService.SendChatChunkAsync(sessionId, assistantMessageId, fallbackAnswer, cancellationToken);
            }

            var finalContent = accumulatedText.ToString();
            if (string.IsNullOrWhiteSpace(finalContent))
            {
                finalContent = "Xin lỗi, đã xảy ra lỗi trong quá trình tạo phản hồi.";
            }

            var assistantMessage = new ChatMessage
            {
                MessageId = assistantMessageId,
                SessionId = sessionId,
                Role = "Assistant",
                Content = finalContent,
                CreatedAt = DateTime.UtcNow
            };

            await _messageRepository.AddAsync(assistantMessage, cancellationToken);

            var citations = BuildCitations(contextDocs, assistantMessage.MessageId);
            if (citations.Count > 0)
            {
                await _citationRepository.AddRangeAsync(citations, cancellationToken);
            }

            session.UpdatedAt = DateTime.UtcNow;
            _sessionRepository.Update(session);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Fetch loaded citations for DTO formatting (e.g. includes Document name)
            var savedCitations = await _citationRepository.GetQueryable()
                .Include(c => c.Document)
                .Where(c => c.MessageId == assistantMessageId)
                .ToListAsync(cancellationToken);

            var assistantMessageDto = ToDto(assistantMessage);
            var citationDtos = savedCitations.Select(ToDto).ToList();

            // Push completion payload via SignalR
            await _realtimeService.SendChatCompleteAsync(sessionId, assistantMessageDto, citationDtos, cancellationToken);

            return new SendChatMessageResponse(
                ToDto(userMessage),
                assistantMessageDto,
                citationDtos);
        }

        private async Task<List<DocumentModelDto>> FilterActiveCompletedContextAsync(
            IReadOnlyList<DocumentModelDto> candidates,
            Guid datasetId,
            CancellationToken cancellationToken)
        {
            var chunkIds = candidates
                .Select(doc => TryGetGuidMetadata(doc.Metadata, "id"))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();

            if (chunkIds.Count == 0)
            {
                return new List<DocumentModelDto>();
            }

            var activeChunkIds = await _chunkRepository.GetQueryable()
                .AsNoTracking()
                .Where(c => chunkIds.Contains(c.ChunkId) &&
                    c.DatasetId == datasetId &&
                    !c.Document.IsDeleted &&
                    c.Document.Status == "Completed")
                .Select(c => c.ChunkId)
                .ToListAsync(cancellationToken);

            var activeSet = activeChunkIds.ToHashSet();

            return candidates
                .Where(doc =>
                {
                    var chunkId = TryGetGuidMetadata(doc.Metadata, "id");
                    return chunkId.HasValue && activeSet.Contains(chunkId.Value);
                })
                .Take(3)
                .ToList();
        }

        private static List<Citation> BuildCitations(IReadOnlyList<DocumentModelDto> contextDocs, Guid messageId)
        {
            var citations = new List<Citation>(contextDocs.Count);

            foreach (var doc in contextDocs)
            {
                if (!doc.Metadata.TryGetValue("id", out var chunkIdObj) ||
                    !Guid.TryParse(chunkIdObj?.ToString(), out var chunkId) ||
                    !doc.Metadata.TryGetValue("document_id", out var docIdObj) ||
                    !Guid.TryParse(docIdObj?.ToString(), out var docId))
                {
                    continue;
                }

                citations.Add(new Citation
                {
                    CitationId = Guid.NewGuid(),
                    MessageId = messageId,
                    DocumentId = docId,
                    ChunkId = chunkId,
                    PageNumber = GetMetadataInt(doc.Metadata, "page_number") ?? 1,
                    QuoteText = doc.PageContent,
                    SourceLabel = GetMetadataString(doc.Metadata, "file_name") ?? "Chunk Reference",
                    CreatedAt = DateTime.UtcNow
                });
            }

            return citations;
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

        private static CitationDto ToDto(Citation citation)
        {
            return new CitationDto(
                citation.CitationId,
                citation.MessageId,
                citation.ChunkId,
                citation.DocumentId,
                citation.Document?.FileName ?? citation.SourceLabel,
                citation.PageNumber,
                citation.QuoteText,
                citation.SourceLabel,
                citation.CreatedAt);
        }

        private static int? GetMetadataInt(Dictionary<string, object> metadata, string key)
        {
            if (!metadata.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }

            if (value is int intValue) return intValue;
            if (value is long longValue) return checked((int)longValue);
            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var jsonInt))
                {
                    return jsonInt;
                }

                if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var jsonStringInt))
                {
                    return jsonStringInt;
                }
            }

            return int.TryParse(value.ToString(), out var parsed) ? parsed : null;
        }

        private static Guid? TryGetGuidMetadata(Dictionary<string, object> metadata, string key)
        {
            return metadata.TryGetValue(key, out var value) && Guid.TryParse(value?.ToString(), out var parsed)
                ? parsed
                : null;
        }

        private static string? GetMetadataString(Dictionary<string, object> metadata, string key)
        {
            if (!metadata.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }

            return value is JsonElement element && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : value.ToString();
        }
    }
}
