using System.Threading;
using System.Threading.Tasks;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IEmailService
    {
        Task SendAccountCreatedEmailAsync(
            string email,
            string fullName,
            string username,
            string temporaryPassword,
            CancellationToken cancellationToken = default);
    }
}
