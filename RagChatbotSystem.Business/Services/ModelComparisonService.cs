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

        // Danh sách các provider so sánh bao gồm Llama (Groq), Qwen (Groq) và Google Gemini.
        public IReadOnlyList<string> AvailableProviders { get; } = new[] { "Groq", "Qwen", "Gemini" };

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

            var providerAnswers = new List<(string ProviderKey, string ModelName, LlmAnswerResult? Answer, long LatencyMs, string? ErrorMessage)>();
            foreach (var providerKey in providerKeys.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var provider = CreateProvider(providerKey);
                if (provider == null)
                {
                    providerAnswers.Add((providerKey, "-", null, 0, "Provider không được hỗ trợ."));
                    continue;
                }

                var modelName = provider.ModelName;
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var answer = await provider.GenerateAnswerWithUsageAsync(prompt);
                    stopwatch.Stop();
                    providerAnswers.Add((providerKey, modelName, answer, stopwatch.ElapsedMilliseconds, null));
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogWarning(ex, "Model comparison call failed for provider {Provider}", providerKey);
                    providerAnswers.Add((providerKey, modelName, null, stopwatch.ElapsedMilliseconds, ex.Message));
                }
            }

            // Chấm điểm so sánh trực tiếp giữa các câu trả lời thành công trong CÙNG 1 lần gọi giám khảo,
            // thay vì chấm từng câu độc lập — ép giám khảo phải phân biệt chất lượng thay vì cho điểm na ná nhau.
            var successfulAnswers = providerAnswers
                .Where(p => p.Answer != null && p.Answer.IsSuccess)
                .Select(p => (p.ProviderKey, Answer: p.Answer!.Content))
                .ToList();
            var judgeScores = await JudgeAllAsync(question, contextText, successfulAnswers, cancellationToken);

            var results = new List<ModelComparisonResultDto>();
            foreach (var pa in providerAnswers)
            {
                if (pa.Answer == null)
                {
                    results.Add(new ModelComparisonResultDto(pa.ProviderKey, pa.ModelName, string.Empty, pa.LatencyMs, 0, 0, 0, false, pa.ErrorMessage, null, null, null, null));
                    continue;
                }

                var (score, reasoning) = judgeScores.TryGetValue(pa.ProviderKey, out var js) ? js : (null, null);

                // Chấm điểm khách quan bằng embedding cosine (chuẩn RAGAS) qua RAG API.
                // Best-effort: nếu RAG API lỗi thì để null, không làm sập luồng so sánh.
                double? faithfulness = null, relevance = null;
                if (pa.Answer.IsSuccess && !string.IsNullOrWhiteSpace(pa.Answer.Content))
                {
                    var embedScore = await _ragApiClient.ScoreAsync(new ScoreRequestDto
                    {
                        Answer = pa.Answer.Content,
                        Context = contextText,
                        Question = question
                    });
                    if (embedScore != null)
                    {
                        faithfulness = Math.Round(embedScore.Faithfulness, 3);
                        relevance = Math.Round(embedScore.Relevance, 3);
                    }
                }

                results.Add(new ModelComparisonResultDto(
                    pa.ProviderKey,
                    pa.Answer.ModelName,
                    pa.Answer.Content,
                    pa.LatencyMs,
                    pa.Answer.InputTokens,
                    pa.Answer.OutputTokens,
                    pa.Answer.TotalTokens,
                    pa.Answer.IsSuccess,
                    pa.Answer.ErrorMessage,
                    score,
                    reasoning,
                    faithfulness,
                    relevance));
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
                    res.QualityReasoning,
                    res.Faithfulness,
                    res.Relevance)).ToList())).ToList();
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
                .Where(res => runIds.Contains(res.ModelComparisonRunId))
                .GroupBy(res => res.ProviderKey)
                .Select(g => new ModelComparisonProviderStatDto(
                    g.Key,
                    g.Count(),
                    g.Average(x => x.IsSuccess ? (double?)x.LatencyMs : null) ?? 0,
                    g.Average(x => x.IsSuccess ? (double?)x.QualityScore : null),
                    g.Average(x => x.IsSuccess ? (double?)x.TotalTokens : null) ?? 0,
                    g.Average(x => x.IsSuccess ? x.Faithfulness : null),
                    g.Average(x => x.IsSuccess ? x.Relevance : null)))
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
                    QualityReasoning = r.QualityReasoning,
                    Faithfulness = r.Faithfulness,
                    Relevance = r.Relevance
                }).ToList()
            };

            await _runRepository.AddAsync(run, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<Dictionary<string, (int? Score, string? Reasoning)>> JudgeAllAsync(
            string question,
            string contextText,
            IReadOnlyList<(string ProviderKey, string Answer)> candidates,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, (int? Score, string? Reasoning)>();
            if (candidates.Count == 0)
            {
                return result;
            }

            try
            {
                var promptBuilder = new System.Text.StringBuilder();
                promptBuilder.AppendLine("Bạn là giám khảo chấm điểm SO SÁNH các câu trả lời AI cho CÙNG một câu hỏi, dựa trên CÙNG một ngữ cảnh.");
                promptBuilder.AppendLine("Chấm mỗi câu theo thang điểm 1-10 dựa trên: độ chính xác, đầy đủ, mức độ bám sát ngữ cảnh (không cộng điểm cho nội dung không có trong ngữ cảnh dù đúng kiến thức chung). Hãy so sánh trực tiếp giữa các câu trả lời — chỉ cho điểm bằng nhau nếu chất lượng thực sự ngang nhau, đừng né tránh sự khác biệt.");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Trả lời đúng định dạng sau, mỗi câu 1 dòng, không thêm gì khác:");
                foreach (var candidate in candidates)
                {
                    promptBuilder.AppendLine($"{candidate.ProviderKey}: Điểm: X. Lý do: <giải thích ngắn gọn>");
                }
                promptBuilder.AppendLine();
                promptBuilder.AppendLine($"Ngữ cảnh:\n{contextText}");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine($"Câu hỏi: {question}");
                promptBuilder.AppendLine();
                foreach (var candidate in candidates)
                {
                    promptBuilder.AppendLine($"--- Câu trả lời của {candidate.ProviderKey} ---");
                    promptBuilder.AppendLine(candidate.Answer);
                    promptBuilder.AppendLine();
                }

                var judge = new GroqService(_httpClientFactory.CreateClient("ModelComparison.Groq"), _configuration, _groqLogger, JudgeModel);
                var judgeResponse = await judge.GenerateAnswerAsync(promptBuilder.ToString());

                foreach (var candidate in candidates)
                {
                    var pattern = $@"^{Regex.Escape(candidate.ProviderKey)}:\s*Điểm:\s*(\d{{1,2}}).*?Lý do:\s*(.+)$";
                    var match = Regex.Match(judgeResponse, pattern, RegexOptions.Multiline);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var score))
                    {
                        result[candidate.ProviderKey] = (Math.Clamp(score, 1, 10), match.Groups[2].Value.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Comparative judge scoring failed, skipping quality scores for this run.");
            }

            return result;
        }

        private ILlmService? CreateProvider(string providerKey)
        {
            return providerKey switch
            {
                "Groq" => new GroqService(_httpClientFactory.CreateClient("ModelComparison.Groq"), _configuration, _groqLogger),
                "Qwen" => new GroqService(_httpClientFactory.CreateClient("ModelComparison.Groq"), _configuration, _groqLogger, QwenModel),
                "Gemini" => new LlmService(_httpClientFactory.CreateClient("ModelComparison.Gemini"), _configuration),
                _ => null
            };
        }
    }
}
