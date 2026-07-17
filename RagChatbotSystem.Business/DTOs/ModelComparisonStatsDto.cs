using System.Collections.Generic;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record ModelComparisonProviderStatDto(
        string ProviderKey,
        int RunCount,
        double AvgLatencyMs,
        double? AvgQualityScore,
        double AvgTotalTokens,
        double? AvgFaithfulness,
        double? AvgRelevance);

    public sealed record ModelComparisonStatsDto(
        IReadOnlyList<ModelComparisonProviderStatDto> ProviderStats);
}
