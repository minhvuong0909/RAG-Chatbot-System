using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;

        public UserService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AsNoTracking()
                .OrderBy(u => u.FullName)
                .Select(u => new UserDto(
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.CreatedAt))
                .ToListAsync(cancellationToken);
        }

        public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new UserDto(
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                throw new ArgumentException("Full name is required.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ArgumentException("Email is required.", nameof(request));
            }

            var email = request.Email.Trim();
            var emailExists = await _context.Users.AnyAsync(u => u.Email == email, cancellationToken);
            if (emailExists)
            {
                throw new InvalidOperationException("A user with this email already exists.");
            }

            var user = new User
            {
                UserId = Guid.NewGuid(),
                FullName = request.FullName.Trim(),
                Email = email,
                Role = string.IsNullOrWhiteSpace(request.Role) ? "User" : request.Role.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            return ToDto(user);
        }

        private static UserDto ToDto(User user)
        {
            return new UserDto(user.UserId, user.FullName, user.Email, user.Role, user.CreatedAt);
        }
    }
}
