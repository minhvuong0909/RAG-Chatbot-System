using System.Collections.Generic;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record ModelComparisonProviderStatDto(
        string ProviderKey,
        int RunCount,
        double AvgLatencyMs,
        double? AvgQualityScore,
        double AvgTotalTokens);

    public sealed record ModelComparisonStatsDto(
        IReadOnlyList<ModelComparisonProviderStatDto> ProviderStats);
}
