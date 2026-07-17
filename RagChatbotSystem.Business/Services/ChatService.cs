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
using RagChatbotSystem.Business.Exceptions;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Business.Services
{
    public class ChatService : IChatService
    {
        private const string FriendlyAiUnavailableMessage = "Hiện chưa thể tạo câu trả lời từ AI. Vui lòng thử lại sau hoặc liên hệ quản trị viên.";
        private const int MaxRelevantContextChunks = 5;
        private const int MaxOverviewDocuments = 12;
        private const int MaxOverviewContextChunks = 12;
        private const int OverviewChunksPerDocument = 2;

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
                .Include(s => s.Dataset)
                    .ThenInclude(d => d.DatasetPermissions)
                .Include(s => s.Dataset)
                    .ThenInclude(d => d.TeacherSubjectAssignment)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
            if (session == null)
            {
                throw new ArgumentException("Chat session not found.");
            }

            if (session.Dataset.IsArchived)
            {
                throw new UnauthorizedAccessException("Subject is archived.");
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
                    throw new ChatRequestBlockedException(
                        ChatBlockReason.DailyTokenLimit,
                        "Bạn đã dùng hết hạn mức AI hôm nay. Bạn vẫn có thể xem lịch sử chat và nguồn dẫn chứng. Vui lòng quay lại vào ngày mai hoặc liên hệ giảng viên/quản trị viên.");
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
                    throw new ChatRequestBlockedException(
                        ChatBlockReason.InsufficientCredits,
                        "Bạn đã hết Credit. Vui lòng nạp thêm Credit để tiếp tục đặt câu hỏi.");
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

            var datasetIdStr = session.DatasetId.ToString();
            var isDatasetOverviewQuestion = IsDatasetOverviewQuestion(userQuestion);
            List<DocumentModelDto> contextDocs;

            if (isDatasetOverviewQuestion)
            {
                contextDocs = await GetDatasetOverviewContextAsync(session.DatasetId, cancellationToken);
                _logger.LogInformation(
                    "Built balanced overview context with {ChunkCount} chunks across {DocumentCount} documents for DatasetId '{DatasetId}'.",
                    contextDocs.Count,
                    contextDocs.Select(doc => TryGetGuidMetadata(doc.Metadata, "document_id")).Where(id => id.HasValue).Distinct().Count(),
                    datasetIdStr);
            }
            else
            {
                var retrieveResult = await _ragApiClient.RetrieveAsync(new RetrieveRequestDto
                {
                    Query = userQuestion,
                    DatasetId = session.DatasetId,
                    TopK = 10,
                    SemanticWeight = 0.7,
                    LexicalWeight = 0.3,
                    EnableRerank = true
                });

                _logger.LogInformation("Retrieve returned {Count} documents from RAG API for query '{Query}'. Filter DatasetId: '{DatasetId}'",
                    retrieveResult.Documents?.Count ?? 0, userQuestion, datasetIdStr);

                var retrievedForDataset = (retrieveResult.Documents ?? Enumerable.Empty<DocumentModelDto>())
                    .Where(doc => doc.Metadata.TryGetValue("dataset_id", out var dsId)
                        && string.Equals(dsId?.ToString(), datasetIdStr, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                contextDocs = await FilterActiveCompletedContextAsync(
                    retrievedForDataset,
                    session.DatasetId,
                    MaxRelevantContextChunks,
                    cancellationToken);
            }

            var isDocumentScopedQuestion = IsDocumentScopedQuestion(userQuestion, contextDocs);
            var contextText = BuildContextText(contextDocs);
            var overviewInstruction = isDatasetOverviewQuestion
                ? "Đây là yêu cầu tổng quan toàn bộ môn học: phải bao quát từng nguồn được cung cấp. Nếu các tài liệu không cùng chủ đề, hãy nói rõ điều đó và tóm tắt riêng từng tài liệu thay vì cố ghép chúng thành một chủ đề giả."
                : "Chỉ giữ các thông tin trực tiếp giúp trả lời câu hỏi.";

            var prompt =
                "Bạn là một trợ lý AI hữu ích. Hãy trả lời bằng tiếng Việt và chỉ dựa trên phần Ngữ cảnh bên dưới.\n" +
                "Hãy tổng hợp, diễn giải và loại bỏ chi tiết nhiễu hoặc trùng lặp. Không chép nguyên văn các chunk thành danh sách và không nhắc đến từ 'chunk' trong câu trả lời.\n" +
                $"{overviewInstruction}\n" +
                "Nếu Ngữ cảnh thực sự không chứa thông tin cần thiết, hãy trả lời: \"Tôi không tìm thấy thông tin này trong tài liệu của bạn.\" Không tự bịa dữ kiện ngoài tài liệu.\n\n" +
                $"Ngữ cảnh:\n{contextText}\n\n" +
                $"Câu hỏi: {userQuestion}\n" +
                "Câu trả lời:";

            var assistantMessageId = Guid.NewGuid();
            var accumulatedText = new StringBuilder();
            LlmAnswerResult? llmResult = null;
            var streamedAnyChunk = false;
            var streamedChunkCount = 0;

            if (!isDocumentScopedQuestion)
            {
                accumulatedText.Append(BuildOutOfScopeAnswer());
                await _realtimeService.SendChatChunkAsync(sessionId, assistantMessageId, accumulatedText.ToString(), cancellationToken);
                streamedAnyChunk = true;
                contextDocs.Clear();
            }
            else
            {
                try
                {
                    await foreach (var chunk in _llmService.GenerateAnswerStreamAsync(prompt).WithCancellation(cancellationToken))
                    {
                        if (string.IsNullOrEmpty(chunk))
                        {
                            continue;
                        }

                        accumulatedText.Append(chunk);
                        streamedAnyChunk = true;
                        streamedChunkCount++;
                        await _realtimeService.SendChatChunkAsync(sessionId, assistantMessageId, chunk, cancellationToken);
                    }

                    var streamedContent = accumulatedText.ToString();
                    var streamCompletedWithoutContent = string.IsNullOrWhiteSpace(streamedContent);
                    llmResult = new LlmAnswerResult(
                        streamedContent,
                        _llmService.ModelName,
                        _llmService.LastPromptTokens,
                        _llmService.LastCompletionTokens,
                        _llmService.LastTotalTokens,
                        _llmService.LastWasActualTokenUsage,
                        !_llmService.LastIsProviderFallback && !streamCompletedWithoutContent,
                        _llmService.LastIsProviderFallback || streamCompletedWithoutContent,
                        streamCompletedWithoutContent ? "LLM stream completed without content." : _llmService.LastErrorMessage);

                    _logger.LogInformation(
                        "LLM stream completed. Provider={Provider}, Model={Model}, IsSuccess={IsSuccess}, IsProviderFallback={IsProviderFallback}, Error={Error}, ContentLength={ContentLength}, InputTokens={InputTokens}, OutputTokens={OutputTokens}, TotalTokens={TotalTokens}, WasActualTokenUsage={WasActualTokenUsage}, RetrievedChunks={RetrievedChunks}, StreamedChunks={StreamedChunks}",
                        _llmService.GetType().Name,
                        llmResult.ModelName,
                        llmResult.IsSuccess,
                        llmResult.IsProviderFallback,
                        llmResult.ErrorMessage,
                        llmResult.Content.Length,
                        llmResult.InputTokens,
                        llmResult.OutputTokens,
                        llmResult.TotalTokens,
                        llmResult.WasActualTokenUsage,
                        contextDocs.Count,
                        streamedChunkCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LLM generation failed.");
                    await _realtimeService.SendChatFailedAsync(
                        sessionId,
                        assistantMessageId,
                        FriendlyAiUnavailableMessage,
                        cancellationToken);

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

                    throw new InvalidOperationException(FriendlyAiUnavailableMessage, ex);
                }
            }

            if (isDocumentScopedQuestion && llmResult != null && (!llmResult.IsSuccess || llmResult.IsProviderFallback))
            {
                _logger.LogWarning(
                    "LLM provider did not return a chargeable answer. Provider={Provider}, Model={Model}, IsSuccess={IsSuccess}, IsProviderFallback={IsProviderFallback}, Error={Error}, ContentLength={ContentLength}, RetrievedChunks={RetrievedChunks}, StreamedChunks={StreamedChunks}",
                    _llmService.GetType().Name,
                    llmResult.ModelName,
                    llmResult.IsSuccess,
                    llmResult.IsProviderFallback,
                    llmResult.ErrorMessage,
                    llmResult.Content.Length,
                    contextDocs.Count,
                    streamedChunkCount);

                await _realtimeService.SendChatFailedAsync(
                    sessionId,
                    assistantMessageId,
                    FriendlyAiUnavailableMessage,
                    cancellationToken);

                if (isStudent)
                {
                    await _creditService.LogBlockedAttemptAsync(
                        session.UserId,
                        CreditBlockedReason.PROVIDER_ERROR,
                        session.DatasetId,
                        sessionId,
                        userQuestion,
                        note: llmResult.ErrorMessage ?? "Provider did not return a valid answer.",
                        cancellationToken: cancellationToken);
                }

                throw new InvalidOperationException(FriendlyAiUnavailableMessage);
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

            var citations = BuildCitations(contextDocs, assistantMessage.MessageId);
            CreditSpendResultDto? creditSpend = null;

            try
            {
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

                    if (isDocumentScopedQuestion && llmResult != null && llmResult.IsSuccess && !llmResult.IsProviderFallback && llmResult.TotalTokens > 0)
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist streamed chat answer.");
                if (streamedAnyChunk)
                {
                    await _realtimeService.SendChatFailedAsync(
                        sessionId,
                        assistantMessageId,
                        "The answer was not saved, so it was not charged. Please try again.",
                        cancellationToken);
                }

                throw;
            }

            // Fetch loaded citations for DTO formatting (e.g. includes Document name)
            var savedCitations = await _citationRepository.GetQueryable()
                .Include(c => c.Document)
                .Where(c => c.MessageId == assistantMessageId)
                .ToListAsync(cancellationToken);

            var assistantMessageDto = ToDto(assistantMessage);
            var citationMetadata = contextDocs
                .Select(doc => new { ChunkId = TryGetGuidMetadata(doc.Metadata, "id"), doc.Metadata })
                .Where(item => item.ChunkId.HasValue)
                .ToDictionary(item => item.ChunkId!.Value, item => item.Metadata);
            var citationDtos = savedCitations
                .Select(citation => ToDto(citation, citationMetadata.GetValueOrDefault(citation.ChunkId)))
                .ToList();

            // Push completion payload via SignalR
            await _realtimeService.SendChatCompleteAsync(sessionId, assistantMessageDto, citationDtos, creditSpend, creditBalance, cancellationToken);
            if (isStudent && creditBalance != null)
            {
                await _realtimeService.SendCreditBalanceChangedAsync(
                    session.UserId,
                    creditBalance,
                    "chat-spend",
                    creditSpend,
                    cancellationToken);
            }

            return new SendChatMessageResponse(
                ToDto(userMessage),
                assistantMessageDto,
                citationDtos,
                creditSpend,
                creditBalance);
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

        private static bool IsDatasetOverviewQuestion(string question)
        {
            var normalized = NormalizeForIntent(question);
            var asksForOverview = normalized.Contains("tom tat")
                || normalized.Contains("y chinh")
                || normalized.Contains("tong quan")
                || normalized.Contains("khai quat");
            var coversCollection = normalized.Contains("mon hoc")
                || normalized.Contains("tat ca tai lieu")
                || normalized.Contains("toan bo tai lieu")
                || normalized.Contains("cac tai lieu")
                || normalized.Contains("tai lieu da tai len");

            return asksForOverview && coversCollection;
        }

        private static string BuildContextText(IReadOnlyList<DocumentModelDto> contextDocs)
        {
            if (contextDocs.Count == 0)
            {
                return "Không tìm thấy tài liệu phù hợp trong ngữ cảnh.";
            }

            return string.Join("\n\n---\n\n", contextDocs.Select(doc =>
            {
                var fileName = GetMetadataString(doc.Metadata, "file_name") ?? "Tài liệu không xác định";
                var fileType = GetMetadataString(doc.Metadata, "file_type");
                var pageNumber = GetMetadataInt(doc.Metadata, "page_number");
                var chunkIndex = GetMetadataInt(doc.Metadata, "chunk_index");
                var location = string.Equals(fileType, "pdf", StringComparison.OrdinalIgnoreCase) && pageNumber > 0
                    ? $"Trang {pageNumber}"
                    : chunkIndex.HasValue ? $"Đoạn {chunkIndex.Value}" : "Đoạn không xác định";
                return $"[Nguồn: {fileName}; {location}]\n{doc.PageContent}";
            }));
        }

        private async Task<List<DocumentModelDto>> GetDatasetOverviewContextAsync(
            Guid datasetId,
            CancellationToken cancellationToken)
        {
            var documents = await _documentRepository.GetQueryable()
                .AsNoTracking()
                .Where(document => document.DatasetId == datasetId && !document.IsDeleted && document.Status == "Completed")
                .OrderBy(document => document.UploadedAt)
                .ThenBy(document => document.FileName)
                .Select(document => new { document.DocumentId, document.FileName, document.FileType })
                .Take(MaxOverviewDocuments)
                .ToListAsync(cancellationToken);

            if (documents.Count == 0)
            {
                return new List<DocumentModelDto>();
            }

            var documentIds = documents.Select(document => document.DocumentId).ToList();
            var candidates = await _chunkRepository.GetQueryable()
                .AsNoTracking()
                .Where(chunk => documentIds.Contains(chunk.DocumentId) && chunk.ChunkIndex <= OverviewChunksPerDocument)
                .OrderBy(chunk => chunk.ChunkIndex)
                .Select(chunk => new
                {
                    chunk.ChunkId,
                    chunk.DocumentId,
                    chunk.ChunkIndex,
                    chunk.Content,
                    chunk.PageNumber
                })
                .ToListAsync(cancellationToken);

            var chunksByDocument = candidates
                .GroupBy(chunk => chunk.DocumentId)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(chunk => chunk.ChunkIndex).Take(OverviewChunksPerDocument).ToList());
            var context = new List<DocumentModelDto>(Math.Min(MaxOverviewContextChunks, documents.Count * OverviewChunksPerDocument));

            for (var round = 0; round < OverviewChunksPerDocument && context.Count < MaxOverviewContextChunks; round++)
            {
                foreach (var document in documents)
                {
                    if (!chunksByDocument.TryGetValue(document.DocumentId, out var documentChunks) || round >= documentChunks.Count)
                    {
                        continue;
                    }

                    var chunk = documentChunks[round];
                    context.Add(new DocumentModelDto
                    {
                        PageContent = chunk.Content,
                        Metadata = new Dictionary<string, object>
                        {
                            ["id"] = chunk.ChunkId.ToString(),
                            ["document_id"] = document.DocumentId.ToString(),
                            ["dataset_id"] = datasetId.ToString(),
                            ["file_name"] = document.FileName,
                            ["file_type"] = document.FileType,
                            ["page_number"] = chunk.PageNumber,
                            ["chunk_index"] = chunk.ChunkIndex
                        }
                    });

                    if (context.Count >= MaxOverviewContextChunks)
                    {
                        break;
                    }
                }
            }

            return context;
        }

        private async Task<List<DocumentModelDto>> FilterActiveCompletedContextAsync(
            IReadOnlyList<DocumentModelDto> candidates,
            Guid datasetId,
            int maxCount,
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
                .Take(maxCount)
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

                var fileType = GetMetadataString(doc.Metadata, "file_type")
                    ?? Path.GetExtension(GetMetadataString(doc.Metadata, "file_name") ?? string.Empty).TrimStart('.');
                var pageNumber = string.Equals(fileType, "pdf", StringComparison.OrdinalIgnoreCase)
                    ? GetMetadataInt(doc.Metadata, "page_number") ?? 0
                    : 0;

                citations.Add(new Citation
                {
                    CitationId = Guid.NewGuid(),
                    MessageId = messageId,
                    DocumentId = docId,
                    ChunkId = chunkId,
                    PageNumber = pageNumber,
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

        private static CitationDto ToDto(Citation citation, Dictionary<string, object>? metadata = null)
        {
            var fileName = citation.Document?.FileName ?? citation.SourceLabel;
            var fileType = citation.Document?.FileType
                ?? GetMetadataString(metadata ?? new Dictionary<string, object>(), "file_type")
                ?? Path.GetExtension(fileName ?? string.Empty).TrimStart('.').ToLowerInvariant();
            return new CitationDto(
                citation.CitationId,
                citation.MessageId,
                citation.ChunkId,
                citation.DocumentId,
                fileName,
                citation.PageNumber,
                citation.QuoteText,
                citation.SourceLabel,
                citation.CreatedAt,
                fileType,
                citation.Chunk?.ChunkIndex ?? (metadata == null ? null : GetMetadataInt(metadata, "chunk_index")));
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
