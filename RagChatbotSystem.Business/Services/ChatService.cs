using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Repositories;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Services
{
    public class ChatService : IChatService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<ChatSession> _sessionRepository;
        private readonly IGenericRepository<ChatMessage> _messageRepository;
        private readonly IGenericRepository<Citation> _citationRepository;
        private readonly IRagApiClient _ragApiClient;
        private readonly ILlmService _llmService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(IUnitOfWork unitOfWork, IRagApiClient ragApiClient, ILlmService llmService, ILogger<ChatService> logger)
        {
            _unitOfWork = unitOfWork;
            _sessionRepository = _unitOfWork.Repository<ChatSession>();
            _messageRepository = _unitOfWork.Repository<ChatMessage>();
            _citationRepository = _unitOfWork.Repository<Citation>();
            _ragApiClient = ragApiClient;
            _llmService = llmService;
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
                .Take(3)
                .ToList();

            var contextText = contextDocs.Count > 0
                ? string.Join("\n\n---\n\n", contextDocs.Select(d => d.PageContent))
                : "Khong tim thay tai lieu phu hop trong ngu canh.";

            var prompt =
                "Bạn là một trợ lý AI hữu ích. Hãy trả lời câu hỏi của người dùng bằng tiếng Việt dựa vào phần Ngữ cảnh được cung cấp dưới đây.\n" +
                "Nếu thông tin không có trong Ngữ cảnh, hãy trả lời là \"Tôi không tìm thấy thông tin này trong tài liệu của bạn.\" và khuyên người dùng bổ sung tài liệu. Không tự bịa ra câu trả lời nằm ngoài tài liệu.\n\n" +
                $"Ngữ cảnh:\n{contextText}\n\n" +
                $"Câu hỏi: {userQuestion}\n" +
                "Câu trả lời:";

            var aiResponse = await _llmService.GenerateAnswerAsync(prompt);

            var assistantMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                SessionId = sessionId,
                Role = "Assistant",
                Content = aiResponse,
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

            return new SendChatMessageResponse(
                ToDto(userMessage),
                ToDto(assistantMessage),
                citations.Select(ToDto).ToList());
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
                citation.Document?.FileName,
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
