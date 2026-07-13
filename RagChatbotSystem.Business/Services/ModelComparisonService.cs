using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Business.Services
{
    public class ModelComparisonService : IModelComparisonService
    {
        // Model riêng cho giám khảo, khác dòng huấn luyện với Llama và Qwen để giảm thiên vị tự chấm.
        private const string JudgeModel = "openai/gpt-oss-120b";

        // Model Qwen dùng cho lựa chọn "Qwen" — cùng hạ tầng Groq (free tier) nhưng khác dòng huấn luyện với Llama.
        private const string QwenModel = "qwen/qwen3-32b";

        private readonly IRagApiClient _ragApiClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<ModelComparisonRun> _runRepository;
        private readonly IGenericRepository<ModelComparisonResult> _resultRepository;
        private readonly ILogger<GroqService> _groqLogger;
        private readonly ILogger<ModelComparisonService> _logger;

        // Đang dùng Qwen (miễn phí qua Groq). Muốn đổi lại Gemini: comment dòng Qwen, bỏ comment dòng Gemini bên dưới.
        public IReadOnlyList<string> AvailableProviders { get; } = new[] { "Groq", "Qwen" };
        // public IReadOnlyList<string> AvailableProviders { get; } = new[] { "Groq", "Gemini" };

        public ModelComparisonService(
            IRagApiClient ragApiClient,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IUnitOfWork unitOfWork,
            ILogger<GroqService> groqLogger,
            ILogger<ModelComparisonService> logger)
        {
            _ragApiClient = ragApiClient;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _unitOfWork = unitOfWork;
            _runRepository = _unitOfWork.Repository<ModelComparisonRun>();
            _resultRepository = _unitOfWork.Repository<ModelComparisonResult>();
            _groqLogger = groqLogger;
            _logger = logger;
        }

        public async Task<ModelComparisonRunResultDto> CompareAsync(
            Guid datasetId,
            string question,
            IReadOnlyList<string> providerKeys,
            Guid runByUserId,
            CancellationToken cancellationToken = default)
        {
            var retrievalStopwatch = Stopwatch.StartNew();
            var retrieveResult = await _ragApiClient.RetrieveAsync(new RetrieveRequestDto
            {
                Query = question,
                DatasetId = datasetId,
                TopK = 10,
                SemanticWeight = 0.7,
                LexicalWeight = 0.3,
                EnableRerank = true
            });
            retrievalStopwatch.Stop();

            var datasetIdStr = datasetId.ToString();
            var contextDocs = (retrieveResult.Documents ?? Enumerable.Empty<DocumentModelDto>())
                .Where(doc => doc.Metadata.TryGetValue("dataset_id", out var dsId)
                    && string.Equals(dsId?.ToString(), datasetIdStr, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var contextText = contextDocs.Count > 0
                ? string.Join("\n\n---\n\n", contextDocs.Select(d => d.PageContent))
                : "Khong tim thay tai lieu phu hop trong ngu canh.";

            var prompt =
                "Bạn là một trợ lý AI hữu ích. Hãy trả lời câu hỏi của người dùng bằng tiếng Việt dựa vào phần Ngữ cảnh được cung cấp dưới đây.\n" +
                "Nếu thông tin không có trong Ngữ cảnh, hãy trả lời là \"Tôi không tìm thấy thông tin này trong tài liệu của bạn.\" và khuyên người dùng bổ sung tài liệu. Không tự bịa ra câu trả lời nằm ngoài tài liệu.\n\n" +
                $"Ngữ cảnh:\n{contextText}\n\n" +
                $"Câu hỏi: {question}\n" +
                "Câu trả lời:";

            var results = new List<ModelComparisonResultDto>();
            foreach (var providerKey in providerKeys.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var provider = CreateProvider(providerKey);
                if (provider == null)
                {
                    results.Add(new ModelComparisonResultDto(providerKey, "-", string.Empty, 0, 0, 0, 0, false, "Provider không được hỗ trợ.", null, null));
                    continue;
                }

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var answer = await provider.GenerateAnswerWithUsageAsync(prompt);
                    stopwatch.Stop();

                    (int? score, string? reasoning) judgeResult = (null, null);
                    if (answer.IsSuccess)
                    {
                        judgeResult = await TryJudgeAsync(question, contextText, answer.Content, cancellationToken);
                    }

                    results.Add(new ModelComparisonResultDto(
                        providerKey,
                        answer.ModelName,
                        answer.Content,
                        stopwatch.ElapsedMilliseconds,
                        answer.InputTokens,
                        answer.OutputTokens,
                        answer.TotalTokens,
                        answer.IsSuccess,
                        answer.ErrorMessage,
                        judgeResult.score,
                        judgeResult.reasoning));
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogWarning(ex, "Model comparison call failed for provider {Provider}", providerKey);
                    results.Add(new ModelComparisonResultDto(
                        providerKey,
                        provider.ModelName,
                        string.Empty,
                        stopwatch.ElapsedMilliseconds,
                        0,
                        0,
                        0,
                        false,
                        ex.Message,
                        null,
                        null));
                }
            }

            await PersistRunAsync(datasetId, question, contextDocs.Count, retrievalStopwatch.ElapsedMilliseconds, runByUserId, results, cancellationToken);

            return new ModelComparisonRunResultDto(question, datasetId, contextDocs.Count, retrievalStopwatch.ElapsedMilliseconds, results);
        }

        public async Task<IReadOnlyList<ModelComparisonRunSummaryDto>> GetHistoryAsync(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            var query = ScopeRunsByRole(userId, role);
            if (query == null)
            {
                return Array.Empty<ModelComparisonRunSummaryDto>();
            }

            var runs = await query
                .Include(r => r.Dataset)
                .Include(r => r.Results)
                .OrderByDescending(r => r.CreatedAt)
                .Take(100)
                .ToListAsync(cancellationToken);

            return runs.Select(r => new ModelComparisonRunSummaryDto(
                r.Id,
                r.Dataset.Name,
                r.Question,
                r.CreatedAt,
                r.RetrievedChunkCount,
                r.RetrievalLatencyMs,
                r.Results.Select(res => new ModelComparisonResultDto(
                    res.ProviderKey,
                    res.ModelName,
                    res.Answer,
                    res.LatencyMs,
                    res.InputTokens,
                    res.OutputTokens,
                    res.TotalTokens,
                    res.IsSuccess,
                    res.ErrorMessage,
                    res.QualityScore,
                    res.QualityReasoning)).ToList())).ToList();
        }

        public async Task<ModelComparisonStatsDto> GetStatsAsync(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            var runQuery = ScopeRunsByRole(userId, role);
            if (runQuery == null)
            {
                return new ModelComparisonStatsDto(Array.Empty<ModelComparisonProviderStatDto>());
            }

            var runIds = runQuery.Select(r => r.Id);

            var grouped = await _resultRepository.GetQueryable().AsNoTracking()
                .Where(res => res.IsSuccess && runIds.Contains(res.ModelComparisonRunId))
                .GroupBy(res => res.ProviderKey)
                .Select(g => new ModelComparisonProviderStatDto(
                    g.Key,
                    g.Count(),
                    g.Average(x => (double)x.LatencyMs),
                    g.Average(x => (double?)x.QualityScore)))
                .ToListAsync(cancellationToken);

            return new ModelComparisonStatsDto(grouped);
        }

        private IQueryable<ModelComparisonRun>? ScopeRunsByRole(Guid userId, string role)
        {
            var query = _runRepository.GetQueryable().AsNoTracking();

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return query;
            }

            if (string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase))
            {
                return query.Where(r => r.Dataset.TeacherSubjectAssignment != null
                    && r.Dataset.TeacherSubjectAssignment.TeacherId == userId);
            }

            return null;
        }

        private async Task PersistRunAsync(
            Guid datasetId,
            string question,
            int retrievedChunkCount,
            long retrievalLatencyMs,
            Guid runByUserId,
            List<ModelComparisonResultDto> results,
            CancellationToken cancellationToken)
        {
            var run = new ModelComparisonRun
            {
                Id = Guid.NewGuid(),
                DatasetId = datasetId,
                RunByUserId = runByUserId,
                Question = question,
                RetrievedChunkCount = retrievedChunkCount,
                RetrievalLatencyMs = retrievalLatencyMs,
                CreatedAt = DateTime.UtcNow,
                Results = results.Select(r => new ModelComparisonResult
                {
                    Id = Guid.NewGuid(),
                    ProviderKey = r.ProviderKey,
                    ModelName = r.ModelName,
                    Answer = r.Answer,
                    LatencyMs = r.LatencyMs,
                    InputTokens = r.InputTokens,
                    OutputTokens = r.OutputTokens,
                    TotalTokens = r.TotalTokens,
                    IsSuccess = r.IsSuccess,
                    ErrorMessage = r.ErrorMessage,
                    QualityScore = r.QualityScore,
                    QualityReasoning = r.QualityReasoning
                }).ToList()
            };

            await _runRepository.AddAsync(run, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<(int? Score, string? Reasoning)> TryJudgeAsync(
            string question,
            string contextText,
            string candidateAnswer,
            CancellationToken cancellationToken)
        {
            try
            {
                var judgePrompt =
                    "Bạn là giám khảo chấm điểm câu trả lời của một trợ lý AI. Dựa vào Ngữ cảnh và Câu hỏi dưới đây, hãy chấm điểm Câu trả lời theo thang điểm từ 1 đến 10 dựa trên độ chính xác, đầy đủ và mức độ liên quan tới Ngữ cảnh.\n" +
                    "Chỉ trả lời đúng theo định dạng sau, không thêm gì khác: \"Điểm: X. Lý do: <giải thích ngắn gọn 1-2 câu>\" (X là số nguyên từ 1 đến 10).\n\n" +
                    $"Ngữ cảnh:\n{contextText}\n\n" +
                    $"Câu hỏi: {question}\n\n" +
                    $"Câu trả lời cần chấm: {candidateAnswer}\n";

                var judge = new GroqService(_httpClientFactory.CreateClient("ModelComparison.Groq"), _configuration, _groqLogger, JudgeModel);
                var judgeResponse = await judge.GenerateAnswerAsync(judgePrompt);

                var scoreMatch = Regex.Match(judgeResponse, @"Điểm:\s*(\d{1,2})");
                if (!scoreMatch.Success || !int.TryParse(scoreMatch.Groups[1].Value, out var score))
                {
                    return (null, null);
                }

                score = Math.Clamp(score, 1, 10);

                var reasonMatch = Regex.Match(judgeResponse, @"Lý do:\s*(.+)", RegexOptions.Singleline);
                var reasoning = reasonMatch.Success ? reasonMatch.Groups[1].Value.Trim() : null;

                return (score, reasoning);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Judge scoring failed, skipping quality score for this result.");
                return (null, null);
            }
        }

        private ILlmService? CreateProvider(string providerKey)
        {
            return providerKey switch
            {
                "Groq" => new GroqService(_httpClientFactory.CreateClient("ModelComparison.Groq"), _configuration, _groqLogger),
                "Qwen" => new GroqService(_httpClientFactory.CreateClient("ModelComparison.Groq"), _configuration, _groqLogger, QwenModel),
                // "Gemini" => new LlmService(_httpClientFactory.CreateClient("ModelComparison.Gemini"), _configuration),
                _ => null
            };
        }
    }
}
