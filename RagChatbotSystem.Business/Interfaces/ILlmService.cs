using System.Threading.Tasks;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface ILlmService
    {
        Task<string> GenerateAnswerAsync(string prompt);
    }
}
