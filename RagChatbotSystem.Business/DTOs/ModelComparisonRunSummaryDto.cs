using System;
using System.Collections.Generic;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record ModelComparisonRunSummaryDto(
        Guid Id,
        string DatasetName,
        string Question,
        DateTime CreatedAt,
        int RetrievedChunkCount,
        long RetrievalLatencyMs,
        IReadOnlyList<ModelComparisonResultDto> Results);
}
