using System.Collections.Generic;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record ModelComparisonProviderStatDto(
        string ProviderKey,
        int RunCount,
        double AvgLatencyMs,
        double? AvgQualityScore);

    public sealed record ModelComparisonStatsDto(
        IReadOnlyList<ModelComparisonProviderStatDto> ProviderStats);
}
