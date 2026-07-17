namespace RagChatbotSystem.Business.DTOs
{
    public sealed record ModelComparisonResultDto(
        string ProviderKey,
        string ModelName,
        string Answer,
        long LatencyMs,
        int InputTokens,
        int OutputTokens,
        int TotalTokens,
        bool IsSuccess,
        string? ErrorMessage,
        int? QualityScore,
        string? QualityReasoning);
}
