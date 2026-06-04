using System;
using System.Collections.Generic;
using System.IO;
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
        Task<UserDto?> AuthenticateUserAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<bool> ApproveUserAsync(Guid userId, bool approve, CancellationToken cancellationToken = default);
        Task<ProvisionedAccountDto> CreateTeacherByAdminAsync(AdminCreateTeacherRequest request, Guid adminUserId, CancellationToken cancellationToken = default);
        Task<StudentImportResult> ImportStudentsFromXlsxAsync(Stream xlsxStream, Guid adminUserId, CancellationToken cancellationToken = default);
        Task<bool> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
    }
}

