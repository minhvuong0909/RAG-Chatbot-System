using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Business.Services
{
    public class QuestionSuggestionService : IQuestionSuggestionService
    {
        private readonly IRagApiClient _ragApiClient;
        private readonly IDocumentService _documentService;
        private readonly ILogger<QuestionSuggestionService> _logger;

        public QuestionSuggestionService(
            IRagApiClient ragApiClient,
            IDocumentService documentService,
            ILogger<QuestionSuggestionService> logger)
        {
            _ragApiClient = ragApiClient;
            _documentService = documentService;
            _logger = logger;
        }

        public async Task<QuestionSuggestionResult> SuggestQuestionsAsync(Guid datasetId, CancellationToken cancellationToken = default)
        {
            try
            {
                var questions = await BuildSuggestedQuestionsAsync(datasetId, cancellationToken);
                return new QuestionSuggestionResult(questions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RAG question suggestions failed for dataset {DatasetId}. Falling back to document names.", datasetId);

                var fallbackQuestions = await BuildFallbackQuestionsFromDocumentsAsync(datasetId, cancellationToken);
                if (fallbackQuestions.Count > 0)
                {
                    return new QuestionSuggestionResult(
                        fallbackQuestions,
                        "RAG API is not available, so suggestions were generated from document names.");
                }

                throw;
            }
        }

        private async Task<IReadOnlyList<string>> BuildSuggestedQuestionsAsync(Guid datasetId, CancellationToken cancellationToken)
        {
            var retrieveResult = await _ragApiClient.RetrieveAsync(new RetrieveRequestDto
            {
                Query = "tong quan khai niem dinh nghia quy trinh vi du noi dung chinh bai hoc",
                TopK = 12,
                SemanticWeight = 0.75,
                LexicalWeight = 0.25,
                EnableRerank = true
            });

            var datasetIdText = datasetId.ToString();
            var docs = retrieveResult.Documents
                .Where(doc => doc.Metadata.TryGetValue("dataset_id", out var dsId)
                    && string.Equals(MetadataToString(dsId), datasetIdText, StringComparison.OrdinalIgnoreCase))
                .Where(doc => !string.IsNullOrWhiteSpace(doc.PageContent))
                .Take(6)
                .ToList();

            if (docs.Count == 0)
            {
                return await BuildFallbackQuestionsFromDocumentsAsync(datasetId, cancellationToken);
            }

            var questions = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddQuestion(questions, seen, "Tom tat cac y chinh cua mon hoc nay theo tai lieu da upload?");
            AddQuestion(questions, seen, "Nhung khai niem nao quan trong nhat trong bo tai lieu nay?");

            foreach (var doc in docs)
            {
                var fileName = GetMetadataString(doc.Metadata, "file_name") ?? "tai lieu nay";
                var focus = BuildQuestionFocus(doc.PageContent);

                if (!string.IsNullOrWhiteSpace(focus))
                {
                    AddQuestion(questions, seen, $"Giai thich ngan gon ve: {focus}?");
                    AddQuestion(questions, seen, $"Cho vi du hoac tinh huong ap dung lien quan den {focus}?");
                }

                AddQuestion(questions, seen, $"Trong file {fileName}, dau la noi dung can ghi nho nhat?");

                if (questions.Count >= 5)
                {
                    break;
                }
            }

            return questions.Take(5).ToList();
        }

        private async Task<IReadOnlyList<string>> BuildFallbackQuestionsFromDocumentsAsync(Guid datasetId, CancellationToken cancellationToken)
        {
            var documents = await _documentService.GetDocumentsByDatasetAsync(datasetId, cancellationToken);
            var completedDocuments = documents
                .Where(d => string.Equals(d.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();

            if (completedDocuments.Count == 0)
            {
                return new[]
                {
                    "Tai lieu nao dang can duoc upload hoac index truoc khi bat dau hoi dap?",
                    "Mon hoc nay gom nhung chu de chinh nao?"
                };
            }

            var questions = new List<string>
            {
                "Tom tat cac y chinh cua mon hoc nay theo tai lieu da upload?",
                "Tao 5 cau hoi on tap quan trong nhat tu cac tai lieu trong mon hoc nay?"
            };

            foreach (var document in completedDocuments)
            {
                questions.Add($"Trong file {document.FileName}, nhung diem nao de ra cau hoi kiem tra nhat?");
            }

            return questions.Take(5).ToList();
        }

        private static void AddQuestion(List<string> questions, HashSet<string> seen, string question)
        {
            if (questions.Count >= 5 || string.IsNullOrWhiteSpace(question))
            {
                return;
            }

            var normalized = question.Trim();
            if (seen.Add(normalized))
            {
                questions.Add(normalized);
            }
        }

        private static string BuildQuestionFocus(string content)
        {
            var clean = string.Join(" ", content.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            if (clean.Length == 0)
            {
                return string.Empty;
            }

            var sentenceEnd = clean.IndexOfAny(new[] { '.', '!', '?', ';', ':' });
            var focus = sentenceEnd > 32 ? clean.Substring(0, sentenceEnd) : clean;
            if (focus.Length > 110)
            {
                focus = focus.Substring(0, 110).Trim();
            }

            return focus.Trim(' ', '-', ',', '.', ':', ';');
        }

        private static string? GetMetadataString(Dictionary<string, object> metadata, string key)
        {
            if (!metadata.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }

            return MetadataToString(value);
        }

        private static string? MetadataToString(object value)
        {
            return value is JsonElement element && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : value.ToString();
        }
    }
}
