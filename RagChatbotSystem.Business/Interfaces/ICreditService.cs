using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface ICreditService
    {
        Task<CreditBalanceDto> GetOrCreateBalanceAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<CreditBalanceDto> GetStudentCreditSummaryAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<CreditBalanceDto> EnsureDailyFreeCreditsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> CanStudentAskAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<int> CalculateCreditsAsync(int inputTokens, int outputTokens, CancellationToken cancellationToken = default);
        Task<CreditSpendResultDto> SpendForChatAnswerAsync(
            Guid userId,
            Guid datasetId,
            Guid chatSessionId,
            Guid assistantMessageId,
            int inputTokens,
            int outputTokens,
            int totalTokens,
            string? modelName,
            bool wasActualTokenUsage,
            CancellationToken cancellationToken = default);
        Task<CreditBalanceDto> AddPaidCreditsAsync(Guid userId, int credits, Guid? createdByUserId, string? note, Guid? packageId = null, CancellationToken cancellationToken = default);
        Task<CreditBalanceDto> GrantFreeCreditsAsync(Guid userId, int credits, Guid? createdByUserId, string? note, CancellationToken cancellationToken = default);
        Task<CreditBalanceDto> AdjustCreditsAsync(Guid userId, int freeCreditsDelta, int paidCreditsDelta, Guid? createdByUserId, string note, CancellationToken cancellationToken = default);
        Task LogBlockedAttemptAsync(
            Guid userId,
            CreditBlockedReason reason,
            Guid? datasetId = null,
            Guid? chatSessionId = null,
            string? messagePreview = null,
            int? usedTokensToday = null,
            int? dailyTokenLimit = null,
            string? note = null,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CreditLedgerDto>> GetLedgerAsync(Guid? userId = null, int limit = 100, bool excludeDailyReset = false, CancellationToken cancellationToken = default);
    }
}
