using System.Collections.Generic;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record ModelComparisonProviderStatDto(
        string ProviderKey,
        int RunCount,
        double AvgLatencyMs,
        double? AvgQualityScore,
        double SuccessRatePercent);

    public sealed record ModelComparisonStatsDto(
        IReadOnlyList<ModelComparisonProviderStatDto> ProviderStats);
}
