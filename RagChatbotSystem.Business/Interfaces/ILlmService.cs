using System.Collections.Generic;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface ILlmService
    {
        Task<string> GenerateAnswerAsync(string prompt);
        Task<LlmAnswerResult> GenerateAnswerWithUsageAsync(string prompt);
        IAsyncEnumerable<string> GenerateAnswerStreamAsync(string prompt);
        int LastPromptTokens { get; }
        int LastCompletionTokens { get; }
        int LastTotalTokens { get; }
    }
}
