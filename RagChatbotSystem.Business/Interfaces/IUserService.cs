using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IUserService
    {
        Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default);
        Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    }
}
