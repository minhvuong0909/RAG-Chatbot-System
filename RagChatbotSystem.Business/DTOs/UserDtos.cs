using System;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record UserDto(
        Guid UserId,
        string FullName,
        string Email,
        string Username,
        string Role,
        DateTime CreatedAt,
        bool IsApproved,
        bool MustChangePassword);

    public sealed record CreateUserRequest(
        string FullName,
        string Email,
        string? Role,
        string Password);

    public sealed record AdminCreateTeacherRequest(
        string FullName,
        string Email,
        IReadOnlyList<Guid> DatasetIds);

    public sealed record ProvisionedAccountDto(
        Guid UserId,
        string FullName,
        string Email,
        string Username,
        string Role,
        string TemporaryPassword);

    public sealed record StudentImportRowResult(
        int RowNumber,
        string Email,
        string? FullName,
        bool Success,
        string? Username,
        string? ErrorMessage);

    public sealed record StudentImportResult(
        int TotalRows,
        int CreatedCount,
        int FailedCount,
        IReadOnlyList<StudentImportRowResult> Rows);

    public sealed record ChangePasswordRequest(
        Guid UserId,
        string CurrentPassword,
        string NewPassword,
        string ConfirmNewPassword);
}
