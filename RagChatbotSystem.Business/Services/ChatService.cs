using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Services
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _context;
        private readonly IRagApiClient _ragApiClient;
        private readonly ILlmService _llmService;

        public ChatService(AppDbContext context, IRagApiClient ragApiClient, ILlmService llmService)
        {
            _context = context;
            _ragApiClient = ragApiClient;
            _llmService = llmService;
        }

        public async Task<SendChatMessageResponse> SendChatMessageAsync(Guid sessionId, string userQuestion)
        {
            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                throw new ArgumentException("Question is required.", nameof(userQuestion));
            }

            var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
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

            _context.ChatMessages.Add(userMessage);

            var retrieveResult = await _ragApiClient.RetrieveAsync(new RetrieveRequestDto
            {
                Query = userQuestion,
                TopK = 10,
                SemanticWeight = 0.7,
                LexicalWeight = 0.3,
                EnableRerank = true
            });

            var datasetIdStr = session.DatasetId.ToString();
            var contextDocs = retrieveResult.Documents
                .Where(doc => doc.Metadata.TryGetValue("dataset_id", out var dsId)
                    && string.Equals(dsId?.ToString(), datasetIdStr, StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();

            var contextText = contextDocs.Count > 0
                ? string.Join("\n\n---\n\n", contextDocs.Select(d => d.PageContent))
                : "Khong tim thay tai lieu phu hop trong ngu canh.";

            var prompt =
                "Ban la mot tro ly AI huu ich. Hay tra loi cau hoi cua nguoi dung bang tieng Viet dua vao phan Ngu canh duoc cung cap duoi day.\n" +
                "Neu thong tin khong co trong Ngu canh, hay tra loi la \"Toi khong tim thay thong tin nay trong tai lieu cua ban.\" va khuyen nguoi dung bo sung tai lieu. Khong tu bia ra cau tra loi nam ngoai tai lieu.\n\n" +
                $"Ngu canh:\n{contextText}\n\n" +
                $"Cau hoi: {userQuestion}\n" +
                "Cau tra loi:";

            var aiResponse = await _llmService.GenerateAnswerAsync(prompt);

            var assistantMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                SessionId = sessionId,
                Role = "Assistant",
                Content = aiResponse,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(assistantMessage);

            var citations = BuildCitations(contextDocs, assistantMessage.MessageId);
            if (citations.Count > 0)
            {
                _context.Citations.AddRange(citations);
            }

            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

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
                citation.DocumentId,
                citation.ChunkId,
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
