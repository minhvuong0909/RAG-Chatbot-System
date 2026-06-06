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
        int DocumentCount,
        bool IsPublic,
        bool IsApproved,
        Guid? AssignedTeacherId = null,
        string? AssignedTeacherName = null);

    public sealed record CreateDatasetRequest(
        string Name,
        string? Description,
        Guid CreatedBy,
        bool IsPublic);

    public sealed record TeacherSubjectAssignmentDto(
        Guid AssignmentId,
        Guid TeacherId,
        string TeacherName,
        string TeacherEmail,
        Guid DatasetId,
        string DatasetName,
        Guid AssignedBy,
        DateTime AssignedAt);
}
