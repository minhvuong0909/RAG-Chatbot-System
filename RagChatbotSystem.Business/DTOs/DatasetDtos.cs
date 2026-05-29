using System;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record DatasetDto(
        Guid DatasetId,
        string Name,
        string? Description,
        Guid CreatedBy,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        int DocumentCount);

    public sealed record CreateDatasetRequest(
        string Name,
        string? Description,
        Guid CreatedBy);
}
