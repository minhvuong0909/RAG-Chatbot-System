using System;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record DocumentDto(
        Guid DocumentId,
        Guid DatasetId,
        string FileName,
        string FilePath,
        string FileType,
        long FileSize,
        string Status,
        Guid UploadedBy,
        DateTime UploadedAt,
        DateTime UpdatedAt);

    public sealed record StoredFileDto(
        string OriginalFileName,
        string StoredFileName,
        string RelativePath,
        string FileType,
        long FileSize);

    public sealed record ChunkDto(
        Guid ChunkId,
        Guid DocumentId,
        int ChunkIndex,
        string Content,
        int PageNumber,
        float[]? Embedding);

    public sealed record DocumentChunkPreviewDto(
        Guid ChunkId,
        int ChunkIndex,
        int PageNumber,
        string Content,
        string? MetadataJson);

    public sealed record DocumentPreviewDto(
        Guid DocumentId,
        Guid DatasetId,
        string DatasetName,
        string FileName,
        string FileType,
        long FileSize,
        string Status,
        DateTime UploadedAt,
        IReadOnlyList<DocumentChunkPreviewDto> Chunks);
}
