using System;
using System.Threading.Tasks;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IAccountService
    {
        Task<User> FindOrCreateGoogleUserAsync(string email, string fullName, IEnumerable<string>? adminEmails = null);
        Task<User?> GetUserByIdAsync(Guid userId);
    }
}
