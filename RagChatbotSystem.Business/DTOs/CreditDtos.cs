using System;
using System.Collections.Generic;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record CreditSettingsDto(
        int DailyFreeCredits,
        int CreditTokenUnit,
        int CreditOutputTokenWeight,
        bool EnableCreditSystem);

    public sealed record CreditBalanceDto(
        Guid UserId,
        int FreeCredits,
        int PaidCredits,
        int TotalCredits,
        DateTime LastFreeCreditResetDate,
        CreditSettingsDto Settings);

    public sealed record CreditSpendResultDto(
        int CalculatedCredits,
        int ChargedCredits,
        int FreeCreditsUsed,
        int PaidCreditsUsed,
        int BalanceAfterFree,
        int BalanceAfterPaid,
        bool WasInsufficientBalance,
        bool WasActualTokenUsage,
        string? ModelName);

    public sealed record CreditPackageDto(
        Guid Id,
        string Name,
        string Description,
        int BaseCredits,
        int BonusCredits,
        int TotalCredits,
        decimal Price,
        string Currency,
        bool IsActive,
        int DisplayOrder);

    public sealed record CreditPurchaseDto(
        Guid Id,
        Guid UserId,
        Guid? PackageId,
        int BaseCredits,
        int BonusCredits,
        int TotalCredits,
        decimal Amount,
        string Currency,
        CreditPurchaseStatus Status,
        string? PaymentProvider,
        string? ProviderReference,
        DateTime CreatedAt,
        DateTime? CompletedAt,
        Guid? CreatedByUserId);

    public sealed record CreditLedgerDto(
        Guid Id,
        Guid UserId,
        Guid? DatasetId,
        Guid? ChatSessionId,
        Guid? ChatMessageId,
        CreditLedgerType Type,
        int CalculatedCredits,
        int ChargedCredits,
        int FreeCreditsUsed,
        int PaidCreditsUsed,
        int FreeCreditsAdded,
        int PaidCreditsAdded,
        int BalanceAfterFree,
        int BalanceAfterPaid,
        int InputTokens,
        int OutputTokens,
        int TotalTokens,
        string? ModelName,
        bool WasActualTokenUsage,
        bool WasInsufficientBalance,
        string? Note,
        DateTime CreatedAt);

    public sealed record CreditUsageSummaryDto
    {
        public int TotalCalculatedCredits { get; init; }
        public int TotalChargedCredits { get; init; }
        public int FreeCreditsConsumed { get; init; }
        public int PaidCreditsConsumed { get; init; }
        public int InsufficientBalanceCount { get; init; }
        public int ZeroBalanceBlockedCount { get; init; }
    }

    public sealed record DailyCreditUsageDto
    {
        public DateTime Date { get; init; }
        public string FormattedDate { get; init; } = string.Empty;
        public int CalculatedCredits { get; init; }
        public int ChargedCredits { get; init; }
        public int FreeCreditsUsed { get; init; }
        public int PaidCreditsUsed { get; init; }
    }

    public sealed record CreditLeaderboardDto
    {
        public Guid UserId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public int ChargedCredits { get; init; }
        public int CalculatedCredits { get; init; }
    }

    public sealed record DatasetCreditUsageDto
    {
        public Guid DatasetId { get; init; }
        public string DatasetName { get; init; } = string.Empty;
        public int ChargedCredits { get; init; }
        public int CalculatedCredits { get; init; }
    }

    public sealed record ModelCreditUsageDto
    {
        public string ModelName { get; init; } = string.Empty;
        public int ChargedCredits { get; init; }
        public int CalculatedCredits { get; init; }
    }

    public sealed record CreditReportDto(
        CreditUsageSummaryDto Summary,
        IReadOnlyList<DailyCreditUsageDto> DailyUsage,
        IReadOnlyList<CreditLeaderboardDto> TopStudents,
        IReadOnlyList<DatasetCreditUsageDto> TopDatasets,
        IReadOnlyList<ModelCreditUsageDto> TopModels);
}
