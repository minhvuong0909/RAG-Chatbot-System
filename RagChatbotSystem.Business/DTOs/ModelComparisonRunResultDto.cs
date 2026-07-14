using System;
using System.Collections.Generic;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record ModelComparisonRunResultDto(
        string Question,
        Guid DatasetId,
        int RetrievedChunkCount,
        long RetrievalLatencyMs,
        IReadOnlyList<ModelComparisonResultDto> Results);
}
