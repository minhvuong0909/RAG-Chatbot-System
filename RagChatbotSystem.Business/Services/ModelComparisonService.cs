using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Business.Services
{
    public class ModelComparisonService : IModelComparisonService
    {
        private readonly IRagApiClient _ragApiClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GroqService> _groqLogger;
        private readonly ILogger<ModelComparisonService> _logger;

        public IReadOnlyList<string> AvailableProviders { get; } = new[] { "Groq", "Gemini", "Ollama" };

        public ModelComparisonService(
            IRagApiClient ragApiClient,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GroqService> groqLogger,
            ILogger<ModelComparisonService> logger)
        {
            _ragApiClient = ragApiClient;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _groqLogger = groqLogger;
            _logger = logger;
        }

        public async Task<ModelComparisonRunResultDto> CompareAsync(
            Guid datasetId,
            string question,
            IReadOnlyList<string> providerKeys,
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
                    results.Add(new ModelComparisonResultDto(providerKey, "-", string.Empty, 0, 0, 0, 0, false, "Provider không được hỗ trợ."));
                    continue;
                }

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var answer = await provider.GenerateAnswerWithUsageAsync(prompt);
                    stopwatch.Stop();

                    results.Add(new ModelComparisonResultDto(
                        providerKey,
                        answer.ModelName,
                        answer.Content,
                        stopwatch.ElapsedMilliseconds,
                        answer.InputTokens,
                        answer.OutputTokens,
                        answer.TotalTokens,
                        answer.IsSuccess,
                        answer.ErrorMessage));
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
                        ex.Message));
                }
            }

            return new ModelComparisonRunResultDto(question, datasetId, contextDocs.Count, retrievalStopwatch.ElapsedMilliseconds, results);
        }

        private ILlmService? CreateProvider(string providerKey)
        {
            return providerKey switch
            {
                "Groq" => new GroqService(_httpClientFactory.CreateClient("ModelComparison.Groq"), _configuration, _groqLogger),
                "Gemini" => new LlmService(_httpClientFactory.CreateClient("ModelComparison.Gemini"), _configuration),
                "Ollama" => new OllamaService(_httpClientFactory.CreateClient("ModelComparison.Ollama"), _configuration),
                _ => null
            };
        }
    }
}
