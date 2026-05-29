using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.Constants;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Services
{
    public class AccountService : IAccountService
    {
        private readonly AppDbContext _context;

        public AccountService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User> FindOrCreateGoogleUserAsync(string email, string fullName, IEnumerable<string>? adminEmails = null)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.", nameof(email));
            }

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var role = IsAdminEmail(normalizedEmail, adminEmails)
                ? UserRoles.Admin
                : UserRoles.User;

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user != null)
            {
                if (role == UserRoles.Admin && !string.Equals(user.Role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
                {
                    user.Role = UserRoles.Admin;
                    await _context.SaveChangesAsync();
                }

                return user;
            }

            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = normalizedEmail,
                FullName = string.IsNullOrWhiteSpace(fullName) ? normalizedEmail : fullName.Trim(),
                Role = role,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        public Task<User?> GetUserByIdAsync(Guid userId)
        {
            return _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        }

        private static bool IsAdminEmail(string normalizedEmail, IEnumerable<string>? adminEmails)
        {
            return adminEmails?
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim().ToLowerInvariant())
                .Contains(normalizedEmail) == true;
        }
    }
}
