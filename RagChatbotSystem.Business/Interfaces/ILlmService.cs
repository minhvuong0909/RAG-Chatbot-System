using System.Collections.Generic;
using System.Threading.Tasks;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface ILlmService
    {
        Task<string> GenerateAnswerAsync(string prompt);
        IAsyncEnumerable<string> GenerateAnswerStreamAsync(string prompt);
        int LastPromptTokens { get; }
        int LastCompletionTokens { get; }
        int LastTotalTokens { get; }
    }
}
