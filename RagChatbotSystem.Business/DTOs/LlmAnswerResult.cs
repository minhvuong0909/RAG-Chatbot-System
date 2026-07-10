namespace RagChatbotSystem.Business.DTOs
{
    public sealed record LlmAnswerResult(
        string Content,
        string ModelName,
        int InputTokens,
        int OutputTokens,
        int TotalTokens,
        bool WasActualTokenUsage,
        bool IsSuccess,
        bool IsProviderFallback,
        string? ErrorMessage)
    {
        public static LlmAnswerResult Success(
            string content,
            string modelName,
            int inputTokens,
            int outputTokens,
            int totalTokens,
            bool wasActualTokenUsage)
        {
            return new LlmAnswerResult(
                content,
                modelName,
                inputTokens,
                outputTokens,
                totalTokens,
                wasActualTokenUsage,
                true,
                false,
                null);
        }

        public static LlmAnswerResult Fallback(
            string content,
            string modelName,
            int inputTokens,
            int outputTokens,
            string errorMessage)
        {
            return new LlmAnswerResult(
                content,
                modelName,
                inputTokens,
                outputTokens,
                inputTokens + outputTokens,
                false,
                false,
                true,
                errorMessage);
        }
    }
}
