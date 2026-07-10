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
        private readonly ITokenUsageService _tokenUsageService;
        private readonly ICreditService _creditService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IUnitOfWork unitOfWork,
            IRagApiClient ragApiClient,
            ILlmService llmService,
            IRealtimeService realtimeService,
            ITokenUsageService tokenUsageService,
            ICreditService creditService,
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
            _tokenUsageService = tokenUsageService;
            _creditService = creditService;
            _logger = logger;
        }

        public async Task<SendChatMessageResponse> SendChatMessageAsync(Guid sessionId, string userQuestion, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                throw new ArgumentException("Question is required.", nameof(userQuestion));
            }

            var session = await _sessionRepository.GetQueryable()
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
            if (session == null)
            {
                throw new ArgumentException("Chat session not found.");
            }

            var isStudent = session.User != null && string.Equals(session.User.Role, "Student", StringComparison.OrdinalIgnoreCase);
            CreditBalanceDto? creditBalance = null;
            var creditSystemEnabled = true;
            if (isStudent)
            {
                var isLimitExceeded = await _tokenUsageService.IsLimitExceededAsync(session.UserId, cancellationToken);
                if (isLimitExceeded)
                {
                    await _creditService.LogBlockedAttemptAsync(
                        session.UserId,
                        CreditBlockedReason.DAILY_TOKEN_LIMIT,
                        session.DatasetId,
                        sessionId,
                        userQuestion,
                        note: "Daily technical token limit blocked chat before LLM call.",
                        cancellationToken: cancellationToken);
                    throw new InvalidOperationException("Ban da dung het han muc AI hom nay. Ban van co the xem lich su chat va nguon dan chung. Vui long quay lai vao ngay mai hoac lien he giang vien/quan tri vien.");
                }

                creditBalance = await _creditService.GetStudentCreditSummaryAsync(session.UserId, cancellationToken);
                creditSystemEnabled = creditBalance.Settings.EnableCreditSystem;
                if (creditSystemEnabled && creditBalance.TotalCredits <= 0)
                {
                    await _creditService.LogBlockedAttemptAsync(
                        session.UserId,
                        CreditBlockedReason.ZERO_BALANCE,
                        session.DatasetId,
                        sessionId,
                        userQuestion,
                        note: "Student had no available credits before RAG/LLM call.",
                        cancellationToken: cancellationToken);
                    throw new InvalidOperationException("Ban da dung het credits. Vui long nap them credits de tiep tuc dat cau hoi.");
                }
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
            var isDocumentScopedQuestion = IsDocumentScopedQuestion(userQuestion, contextDocs);

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
            LlmAnswerResult? llmResult = null;

            if (!isDocumentScopedQuestion)
            {
                accumulatedText.Append(BuildOutOfScopeAnswer());
                contextDocs.Clear();
            }
            else
            {
                try
                {
                    llmResult = await _llmService.GenerateAnswerWithUsageAsync(prompt);
                    accumulatedText.Append(llmResult.Content);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LLM generation failed.");
                    if (isStudent)
                    {
                        await _creditService.LogBlockedAttemptAsync(
                            session.UserId,
                            CreditBlockedReason.PROVIDER_ERROR,
                            session.DatasetId,
                            sessionId,
                            userQuestion,
                            note: ex.Message,
                            cancellationToken: cancellationToken);
                    }

                    throw new InvalidOperationException("Khong the tao cau tra loi tu AI luc nay. Vui long thu lai sau.");
                }
            }

            var finalContent = accumulatedText.ToString();
            if (string.IsNullOrWhiteSpace(finalContent))
            {
                finalContent = "Xin lỗi, đã xảy ra lỗi trong quá trình tạo phản hồi.";
            }

            if (isDocumentScopedQuestion && contextDocs.Count > 0 && LooksLikeNoInformationAnswer(finalContent))
            {
                finalContent = BuildGroundedFallbackAnswer(contextDocs);
            }

            var assistantMessage = new ChatMessage
            {
                MessageId = assistantMessageId,
                SessionId = sessionId,
                Role = "Assistant",
                Content = finalContent,
                CreatedAt = DateTime.UtcNow
            };

            var citations = BuildCitations(contextDocs, assistantMessage.MessageId);
            CreditSpendResultDto? creditSpend = null;

            await using (var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken))
            {
                await _messageRepository.AddAsync(assistantMessage, cancellationToken);

                if (citations.Count > 0)
                {
                    await _citationRepository.AddRangeAsync(citations, cancellationToken);
                }

                session.UpdatedAt = DateTime.UtcNow;
                _sessionRepository.Update(session);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                if (isDocumentScopedQuestion && llmResult != null && llmResult.TotalTokens > 0)
                {
                    await _tokenUsageService.RecordUsageAsync(session.UserId, session.DatasetId, llmResult.TotalTokens, cancellationToken);
                }

                if (isStudent && creditSystemEnabled && isDocumentScopedQuestion && llmResult != null)
                {
                    if (llmResult.IsSuccess && !llmResult.IsProviderFallback)
                    {
                        creditSpend = await _creditService.SpendForChatAnswerAsync(
                            session.UserId,
                            session.DatasetId,
                            sessionId,
                            assistantMessage.MessageId,
                            llmResult.InputTokens,
                            llmResult.OutputTokens,
                            llmResult.TotalTokens,
                            llmResult.ModelName,
                            llmResult.WasActualTokenUsage,
                            cancellationToken);
                        creditBalance = await _creditService.GetStudentCreditSummaryAsync(session.UserId, cancellationToken);
                    }
                    else
                    {
                        await _creditService.LogBlockedAttemptAsync(
                            session.UserId,
                            CreditBlockedReason.PROVIDER_ERROR,
                            session.DatasetId,
                            sessionId,
                            userQuestion,
                            note: llmResult.ErrorMessage ?? "Provider fallback answer was not charged.",
                            cancellationToken: cancellationToken);
                    }
                }

                await transaction.CommitAsync(cancellationToken);
            }

            // Fetch loaded citations for DTO formatting (e.g. includes Document name)
            var savedCitations = await _citationRepository.GetQueryable()
                .Include(c => c.Document)
                .Where(c => c.MessageId == assistantMessageId)
                .ToListAsync(cancellationToken);

            var assistantMessageDto = ToDto(assistantMessage);
            var citationDtos = savedCitations.Select(ToDto).ToList();

            // Push completion payload via SignalR
            await _realtimeService.SendChatChunkAsync(sessionId, assistantMessageId, finalContent, cancellationToken);
            await _realtimeService.SendChatCompleteAsync(sessionId, assistantMessageDto, citationDtos, cancellationToken);

            return new SendChatMessageResponse(
                ToDto(userMessage),
                assistantMessageDto,
                citationDtos,
                creditSpend,
                creditBalance);
        }

        private static bool LooksLikeNoInformationAnswer(string answer)
        {
            var normalized = answer.ToLowerInvariant();
            return normalized.Contains("không tìm thấy")
                || normalized.Contains("khong tim thay")
                || normalized.Contains("không có thông tin")
                || normalized.Contains("khong co thong tin")
                || normalized.Contains("bổ sung thêm tài liệu")
                || normalized.Contains("bo sung them tai lieu");
        }

        private static bool IsDocumentScopedQuestion(string question, IReadOnlyList<DocumentModelDto> contextDocs)
        {
            var normalized = NormalizeForIntent(question);

            if (LooksLikeSmallTalk(normalized))
            {
                return false;
            }

            if (LooksLikeExternalQuestion(normalized))
            {
                return false;
            }

            var documentIntentKeywords = new[]
            {
                "tai lieu", "file", "doc", "docx", "pdf", "van ban", "noi dung", "upload",
                "mon hoc", "bai hoc", "chu de", "nguon", "trich dan", "theo tai lieu",
                "tom tat", "y chinh", "khai niem", "cau hoi on tap", "tu vung", "giai thich"
            };

            if (documentIntentKeywords.Any(normalized.Contains))
            {
                return true;
            }

            return HasMeaningfulOverlapWithContext(normalized, contextDocs);
        }

        private static bool LooksLikeSmallTalk(string normalizedQuestion)
        {
            var smallTalk = new[]
            {
                "xin chao", "chao", "hello", "hi", "cam on", "thank", "ban la ai"
            };

            return smallTalk.Any(term => normalizedQuestion.Equals(term, StringComparison.Ordinal)
                || normalizedQuestion.StartsWith(term + " ", StringComparison.Ordinal));
        }

        private static bool LooksLikeExternalQuestion(string normalizedQuestion)
        {
            var externalTerms = new[]
            {
                "hom nay", "ngay mai", "hom qua", "thu may", "may gio", "thoi tiet",
                "tin tuc", "gia vang", "ty gia", "bitcoin", "tong thong", "ceo"
            };

            return externalTerms.Any(normalizedQuestion.Contains);
        }

        private static bool HasMeaningfulOverlapWithContext(string normalizedQuestion, IReadOnlyList<DocumentModelDto> contextDocs)
        {
            if (contextDocs.Count == 0)
            {
                return false;
            }

            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "la", "gi", "co", "khong", "nhung", "cac", "cua", "ve", "trong", "nay",
                "hay", "cho", "toi", "biet", "the", "nao", "duoc", "khong"
            };

            var tokens = normalizedQuestion
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => token.Length >= 4 && !stopWords.Contains(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tokens.Count == 0)
            {
                return false;
            }

            var context = NormalizeForIntent(string.Join(" ", contextDocs.Select(d => d.PageContent)));
            var overlap = tokens.Count(context.Contains);
            return overlap >= Math.Min(2, tokens.Count);
        }

        private static string NormalizeForIntent(string value)
        {
            var normalized = value.Trim().ToLowerInvariant();
            var replacements = new Dictionary<string, string>
            {
                ["á"] = "a", ["à"] = "a", ["ả"] = "a", ["ã"] = "a", ["ạ"] = "a",
                ["ă"] = "a", ["ắ"] = "a", ["ằ"] = "a", ["ẳ"] = "a", ["ẵ"] = "a", ["ặ"] = "a",
                ["â"] = "a", ["ấ"] = "a", ["ầ"] = "a", ["ẩ"] = "a", ["ẫ"] = "a", ["ậ"] = "a",
                ["é"] = "e", ["è"] = "e", ["ẻ"] = "e", ["ẽ"] = "e", ["ẹ"] = "e",
                ["ê"] = "e", ["ế"] = "e", ["ề"] = "e", ["ể"] = "e", ["ễ"] = "e", ["ệ"] = "e",
                ["í"] = "i", ["ì"] = "i", ["ỉ"] = "i", ["ĩ"] = "i", ["ị"] = "i",
                ["ó"] = "o", ["ò"] = "o", ["ỏ"] = "o", ["õ"] = "o", ["ọ"] = "o",
                ["ô"] = "o", ["ố"] = "o", ["ồ"] = "o", ["ổ"] = "o", ["ỗ"] = "o", ["ộ"] = "o",
                ["ơ"] = "o", ["ớ"] = "o", ["ờ"] = "o", ["ở"] = "o", ["ỡ"] = "o", ["ợ"] = "o",
                ["ú"] = "u", ["ù"] = "u", ["ủ"] = "u", ["ũ"] = "u", ["ụ"] = "u",
                ["ư"] = "u", ["ứ"] = "u", ["ừ"] = "u", ["ử"] = "u", ["ữ"] = "u", ["ự"] = "u",
                ["ý"] = "y", ["ỳ"] = "y", ["ỷ"] = "y", ["ỹ"] = "y", ["ỵ"] = "y",
                ["đ"] = "d"
            };

            foreach (var replacement in replacements)
            {
                normalized = normalized.Replace(replacement.Key, replacement.Value);
            }

            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            }

            return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static string BuildOutOfScopeAnswer()
        {
            return "Cau hoi nay nam ngoai pham vi tai lieu da upload, nen minh khong dung noi dung trong file de tra loi. Hay hoi ve noi dung, tu vung, y chinh, khai niem, hoac cau hoi on tap tu tai lieu de minh tra loi kem nguon tham khao.";
        }

        private static string BuildGroundedFallbackAnswer(IReadOnlyList<DocumentModelDto> contextDocs)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Dựa trên tài liệu đã upload, các ý chính có thể rút ra là:");

            var points = contextDocs
                .SelectMany(d => d.PageContent
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim()))
                .Where(line => line.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList();

            if (points.Count == 0)
            {
                builder.AppendLine("- Tài liệu có nội dung liên quan, nhưng phần trích xuất hiện tại quá ngắn để tóm tắt chi tiết.");
            }
            else
            {
                foreach (var point in points)
                {
                    builder.AppendLine($"- {point}");
                }
            }

            builder.AppendLine();
            builder.Append("Các nguồn tham khảo đã được gắn ở phần View Sources.");
            return builder.ToString();
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
