using System;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record UserDto(
        Guid UserId,
        string FullName,
        string Email,
        string Role,
        DateTime CreatedAt);

    public sealed record CreateUserRequest(
        string FullName,
        string Email,
        string? Role);
}
