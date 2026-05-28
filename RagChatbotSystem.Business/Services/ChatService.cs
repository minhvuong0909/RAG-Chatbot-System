using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task<ChatMessage> SendChatMessageAsync(Guid sessionId, string userQuestion)
        {
            // 1. Lấy thông tin phiên chat
            var session = await _context.ChatSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null)
            {
                throw new ArgumentException("Chat session not found.");
            }

            var now = DateTime.UtcNow;

            // 2. Lưu tin nhắn người dùng
            var userMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                SessionId = sessionId,
                Role = "User",
                Content = userQuestion,
                CreatedAt = now
            };

            _context.ChatMessages.Add(userMessage);

            // 3. Truy vấn ngữ cảnh từ Python RAG API
            var retrieveRequest = new RetrieveRequestDto
            {
                Query = userQuestion,
                TopK = 10,
                SemanticWeight = 0.7,
                LexicalWeight = 0.3,
                EnableRerank = true
            };

            var retrieveResult = await _ragApiClient.RetrieveAsync(retrieveRequest);

            // Lọc kết quả chỉ lấy đoạn văn thuộc đúng DatasetId của phiên chat
            var datasetIdStr = session.DatasetId.ToString();
            var contextDocs = retrieveResult?.Documents?
                .Where(doc => doc.Metadata.TryGetValue("dataset_id", out var dsId)
                    && string.Equals(dsId?.ToString(), datasetIdStr, StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList() ?? new List<DocumentModelDto>();

            // 4. Xây dựng Prompt cho LLM
            var contextText = contextDocs.Count > 0
                ? string.Join("\n\n---\n\n", contextDocs.Select(d => d.PageContent))
                : "Không tìm thấy tài liệu phù hợp trong ngữ cảnh.";

            var prompt = $"Bạn là một trợ lý AI hữu ích. Hãy trả lời câu hỏi của người dùng bằng tiếng Việt dựa vào phần Ngữ cảnh được cung cấp dưới đây.\n" +
                         $"Nếu thông tin không có trong Ngữ cảnh, hãy trả lời là \"Tôi không tìm thấy thông tin này trong tài liệu của bạn.\" và khuyên người dùng bổ sung tài liệu. Không tự bịa ra câu trả lời nằm ngoài tài liệu.\n\n" +
                         $"Ngữ cảnh:\n{contextText}\n\n" +
                         $"Câu hỏi: {userQuestion}\n" +
                         $"Câu trả lời:";

            // 5. Gọi LLM sinh câu trả lời
            var aiResponse = await _llmService.GenerateAnswerAsync(prompt);

            // 6. Lưu tin nhắn của AI
            var assistantMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid(),
                SessionId = sessionId,
                Role = "Assistant",
                Content = aiResponse,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(assistantMessage);

            // 7. Tạo trích dẫn nguồn (Citations) cho câu trả lời
            if (contextDocs.Count > 0)
            {
                var citations = new List<Citation>(contextDocs.Count);
                foreach (var doc in contextDocs)
                {
                    if (doc.Metadata.TryGetValue("id", out var chunkIdObj) && Guid.TryParse(chunkIdObj?.ToString(), out Guid chunkId) &&
                        doc.Metadata.TryGetValue("document_id", out var docIdObj) && Guid.TryParse(docIdObj?.ToString(), out Guid docId))
                    {
                        citations.Add(new Citation
                        {
                            CitationId = Guid.NewGuid(),
                            MessageId = assistantMessage.MessageId,
                            DocumentId = docId,
                            ChunkId = chunkId,
                            PageNumber = 1,
                            QuoteText = doc.PageContent.Length > 200 ? doc.PageContent[..200] : doc.PageContent,
                            SourceLabel = "Chunk Reference",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                if (citations.Count > 0)
                {
                    _context.Citations.AddRange(citations);
                }
            }

            // Ghi tất cả thay đổi vào DB trong 1 lần duy nhất (tối ưu I/O)
            await _context.SaveChangesAsync();

            return assistantMessage;
        }
    }
}
