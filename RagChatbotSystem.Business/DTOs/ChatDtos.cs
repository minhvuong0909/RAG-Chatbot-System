using System;
using System.Collections.Generic;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record ChatSessionDto(
        Guid SessionId,
        Guid UserId,
        Guid DatasetId,
        string Title,
        DateTime StartedAt,
        DateTime UpdatedAt);

    public sealed record CreateChatSessionRequest(
        Guid UserId,
        Guid DatasetId,
        string? Title);

    public sealed record SendChatMessageRequest(
        string? Content,
        string? Question);

    public sealed record ChatMessageDto(
        Guid MessageId,
        Guid SessionId,
        string Role,
        string Content,
        DateTime CreatedAt);

    public sealed record CitationDto(
        Guid CitationId,
        Guid MessageId,
        Guid ChunkId,
        Guid DocumentId,
        string? FileName,
        int PageNumber,
        string QuoteText,
        string? SourceLabel,
        DateTime CreatedAt,
        string? FileType = null,
        int? ChunkIndex = null);

    public sealed record SendChatMessageResponse(
        ChatMessageDto UserMessage,
        ChatMessageDto AssistantMessage,
        IReadOnlyList<CitationDto> Citations,
        CreditSpendResultDto? CreditSpend = null,
        CreditBalanceDto? CreditBalance = null);
}
