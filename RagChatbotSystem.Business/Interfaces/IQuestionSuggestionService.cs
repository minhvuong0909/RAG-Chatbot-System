using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IQuestionSuggestionService
    {
        Task<QuestionSuggestionResult> SuggestQuestionsAsync(Guid datasetId, CancellationToken cancellationToken = default);
    }

    public sealed record QuestionSuggestionResult(IReadOnlyList<string> Questions, string? Warning = null);
}
