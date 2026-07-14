using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Services
{
    public class CreditService : ICreditService
    {
        private readonly AppDbContext _db;
        private static readonly TimeZoneInfo VietnamTz = ResolveVietnamTimeZone();

        public CreditService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<CreditBalanceDto> GetOrCreateBalanceAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
            var settings = await GetSettingsAsync(cancellationToken);
            var (wallet, _) = await GetOrCreateWalletForUpdateAsync(userId, settings, cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            await CommitIfOwnedAsync(transaction, cancellationToken);
            return ToBalanceDto(wallet, settings);
        }

        public async Task<CreditBalanceDto> GetStudentCreditSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await EnsureDailyFreeCreditsAsync(userId, cancellationToken);
        }

        public async Task<CreditBalanceDto> EnsureDailyFreeCreditsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
            var settings = await GetSettingsAsync(cancellationToken);
            var (wallet, created) = await GetOrCreateWalletForUpdateAsync(userId, settings, cancellationToken);
            if (!created)
            {
                ApplyDailyResetIfNeeded(wallet, settings);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await CommitIfOwnedAsync(transaction, cancellationToken);
            return ToBalanceDto(wallet, settings);
        }

        public async Task<bool> CanStudentAskAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var summary = await EnsureDailyFreeCreditsAsync(userId, cancellationToken);
            return !summary.Settings.EnableCreditSystem || summary.TotalCredits > 0;
        }

        public async Task<int> CalculateCreditsAsync(int inputTokens, int outputTokens, CancellationToken cancellationToken = default)
        {
            var settings = await GetSettingsAsync(cancellationToken);
            return CalculateCredits(inputTokens, outputTokens, settings.CreditTokenUnit, settings.CreditOutputTokenWeight);
        }

        public async Task<CreditSpendResultDto> SpendForChatAnswerAsync(
            Guid userId,
            Guid datasetId,
            Guid chatSessionId,
            Guid assistantMessageId,
            int inputTokens,
            int outputTokens,
            int totalTokens,
            string? modelName,
            bool wasActualTokenUsage,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
            var settings = await GetSettingsAsync(cancellationToken);
            var (wallet, created) = await GetOrCreateWalletForUpdateAsync(userId, settings, cancellationToken);
            if (!created)
            {
                ApplyDailyResetIfNeeded(wallet, settings);
            }

            var beforeFree = wallet.FreeCredits;
            var beforePaid = wallet.PaidCredits;
            var available = beforeFree + beforePaid;
            var calculated = CalculateCredits(inputTokens, outputTokens, settings.CreditTokenUnit, settings.CreditOutputTokenWeight);
            var charged = Math.Min(calculated, available);
            var freeUsed = Math.Min(beforeFree, charged);
            var paidUsed = Math.Min(beforePaid, charged - freeUsed);

            wallet.FreeCredits = beforeFree - freeUsed;
            wallet.PaidCredits = beforePaid - paidUsed;
            wallet.UpdatedAt = DateTime.UtcNow;
            wallet.Version++;

            var insufficient = calculated > available;
            if (insufficient)
            {
                wallet.FreeCredits = 0;
                wallet.PaidCredits = 0;
            }

            _db.CreditLedgers.Add(CreateLedger(
                userId,
                CreditLedgerType.SPEND,
                beforeFree,
                beforePaid,
                wallet.FreeCredits,
                wallet.PaidCredits,
                calculatedCredits: calculated,
                chargedCredits: charged,
                freeUsed: freeUsed,
                paidUsed: paidUsed,
                inputTokens: Math.Max(0, inputTokens),
                outputTokens: Math.Max(0, outputTokens),
                totalTokens: Math.Max(0, totalTokens > 0 ? totalTokens : inputTokens + outputTokens),
                outputTokenWeight: settings.CreditOutputTokenWeight,
                tokenUnit: settings.CreditTokenUnit,
                modelName: modelName,
                wasActualTokenUsage: wasActualTokenUsage,
                wasInsufficientBalance: insufficient,
                datasetId: datasetId,
                chatSessionId: chatSessionId,
                chatMessageId: assistantMessageId));

            await _db.SaveChangesAsync(cancellationToken);
            await CommitIfOwnedAsync(transaction, cancellationToken);

            return new CreditSpendResultDto(
                calculated,
                charged,
                freeUsed,
                paidUsed,
                wallet.FreeCredits,
                wallet.PaidCredits,
                insufficient,
                wasActualTokenUsage,
                modelName);
        }

        public async Task<CreditBalanceDto> AddPaidCreditsAsync(Guid userId, int credits, Guid? createdByUserId, string? note, Guid? packageId = null, CancellationToken cancellationToken = default)
        {
            if (credits <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(credits), "Paid credits must be greater than zero.");
            }

            await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
            var settings = await GetSettingsAsync(cancellationToken);
            var (wallet, _) = await GetOrCreateWalletForUpdateAsync(userId, settings, cancellationToken);
            var beforeFree = wallet.FreeCredits;
            var beforePaid = wallet.PaidCredits;

            wallet.PaidCredits += credits;
            wallet.UpdatedAt = DateTime.UtcNow;
            wallet.Version++;

            _db.CreditLedgers.Add(CreateLedger(
                userId,
                packageId.HasValue ? CreditLedgerType.PURCHASE : CreditLedgerType.ADMIN_GRANT,
                beforeFree,
                beforePaid,
                wallet.FreeCredits,
                wallet.PaidCredits,
                paidAdded: credits,
                relatedPackageId: packageId,
                createdByUserId: createdByUserId,
                note: note));

            await _db.SaveChangesAsync(cancellationToken);
            await CommitIfOwnedAsync(transaction, cancellationToken);
            return ToBalanceDto(wallet, settings);
        }

        public async Task<CreditBalanceDto> GrantFreeCreditsAsync(Guid userId, int credits, Guid? createdByUserId, string? note, CancellationToken cancellationToken = default)
        {
            if (credits <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(credits), "Free credits must be greater than zero.");
            }

            await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
            var settings = await GetSettingsAsync(cancellationToken);
            var (wallet, _) = await GetOrCreateWalletForUpdateAsync(userId, settings, cancellationToken);
            var beforeFree = wallet.FreeCredits;
            var beforePaid = wallet.PaidCredits;

            wallet.FreeCredits += credits;
            wallet.UpdatedAt = DateTime.UtcNow;
            wallet.Version++;

            _db.CreditLedgers.Add(CreateLedger(
                userId,
                CreditLedgerType.FREE_GRANT,
                beforeFree,
                beforePaid,
                wallet.FreeCredits,
                wallet.PaidCredits,
                freeAdded: credits,
                createdByUserId: createdByUserId,
                note: note));

            await _db.SaveChangesAsync(cancellationToken);
            await CommitIfOwnedAsync(transaction, cancellationToken);
            return ToBalanceDto(wallet, settings);
        }

        public async Task<CreditBalanceDto> AdjustCreditsAsync(Guid userId, int freeCreditsDelta, int paidCreditsDelta, Guid? createdByUserId, string note, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                throw new ArgumentException("A note is required for credit adjustments.", nameof(note));
            }

            await using var transaction = await BeginTransactionIfNeededAsync(cancellationToken);
            var settings = await GetSettingsAsync(cancellationToken);
            var (wallet, _) = await GetOrCreateWalletForUpdateAsync(userId, settings, cancellationToken);
            var beforeFree = wallet.FreeCredits;
            var beforePaid = wallet.PaidCredits;
            var afterFree = Math.Max(0, beforeFree + freeCreditsDelta);
            var afterPaid = Math.Max(0, beforePaid + paidCreditsDelta);

            wallet.FreeCredits = afterFree;
            wallet.PaidCredits = afterPaid;
            wallet.UpdatedAt = DateTime.UtcNow;
            wallet.Version++;

            _db.CreditLedgers.Add(CreateLedger(
                userId,
                CreditLedgerType.ADJUSTMENT,
                beforeFree,
                beforePaid,
                afterFree,
                afterPaid,
                freeAdded: Math.Max(0, afterFree - beforeFree),
                paidAdded: Math.Max(0, afterPaid - beforePaid),
                freeUsed: Math.Max(0, beforeFree - afterFree),
                paidUsed: Math.Max(0, beforePaid - afterPaid),
                createdByUserId: createdByUserId,
                note: note));

            await _db.SaveChangesAsync(cancellationToken);
            await CommitIfOwnedAsync(transaction, cancellationToken);
            return ToBalanceDto(wallet, settings);
        }

        public async Task LogBlockedAttemptAsync(
            Guid userId,
            CreditBlockedReason reason,
            Guid? datasetId = null,
            Guid? chatSessionId = null,
            string? messagePreview = null,
            int? usedTokensToday = null,
            int? dailyTokenLimit = null,
            string? note = null,
            CancellationToken cancellationToken = default)
        {
            var wallet = await _db.CreditWallets.AsNoTracking()
                .FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);

            _db.CreditBlockedAttempts.Add(new CreditBlockedAttempt
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DatasetId = datasetId,
                ChatSessionId = chatSessionId,
                Reason = reason,
                FreeCreditsAtTime = wallet?.FreeCredits ?? 0,
                PaidCreditsAtTime = wallet?.PaidCredits ?? 0,
                UsedTokensToday = usedTokensToday,
                DailyTokenLimit = dailyTokenLimit,
                CreatedAt = DateTime.UtcNow,
                MessagePreview = Truncate(messagePreview, 500),
                Note = Truncate(note, 1000)
            });

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<CreditLedgerDto>> GetLedgerAsync(Guid? userId = null, int limit = 100, bool excludeDailyReset = false, CancellationToken cancellationToken = default)
        {
            var query = _db.CreditLedgers.AsNoTracking();
            if (userId.HasValue)
            {
                query = query.Where(l => l.UserId == userId.Value);
            }
            if (excludeDailyReset)
            {
                query = query.Where(l => l.Type != CreditLedgerType.DAILY_RESET);
            }

            return await query
                .OrderByDescending(l => l.CreatedAt)
                .Take(Math.Clamp(limit, 1, 500))
                .Select(ToLedgerDtoExpression())
                .ToListAsync(cancellationToken);
        }

        private async Task<(CreditWallet Wallet, bool Created)> GetOrCreateWalletForUpdateAsync(
            Guid userId,
            CreditSettingsDto settings,
            CancellationToken cancellationToken)
        {
            var existing = await GetWalletForUpdateAsync(userId, cancellationToken);
            if (existing != null)
            {
                return (existing, false);
            }

            var wallet = CreateNewWallet(userId, settings);
            _db.CreditWallets.Add(wallet);
            _db.CreditLedgers.Add(CreateLedger(
                userId,
                CreditLedgerType.DAILY_RESET,
                beforeFree: 0,
                beforePaid: 0,
                afterFree: wallet.FreeCredits,
                afterPaid: wallet.PaidCredits,
                freeAdded: wallet.FreeCredits,
                note: "Initial daily free credit reset."));

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return (wallet, true);
            }
            catch (DbUpdateException ex) when (IsCreditWalletUserUniqueViolation(ex))
            {
                DetachPendingWalletCreation(userId);
                var racedWallet = await GetWalletForUpdateAsync(userId, cancellationToken)
                    ?? throw new InvalidOperationException("Credit wallet was created concurrently but could not be reloaded.", ex);
                return (racedWallet, false);
            }
        }

        private void ApplyDailyResetIfNeeded(CreditWallet wallet, CreditSettingsDto settings)
        {
            var today = GetTodayInVietnam();
            if (wallet.LastFreeCreditResetDate.Date >= today.Date)
            {
                return;
            }

            var beforeFree = wallet.FreeCredits;
            var beforePaid = wallet.PaidCredits;
            wallet.FreeCredits = settings.DailyFreeCredits;
            wallet.LastFreeCreditResetDate = today;
            wallet.UpdatedAt = DateTime.UtcNow;
            wallet.Version++;

            _db.CreditLedgers.Add(CreateLedger(
                wallet.UserId,
                CreditLedgerType.DAILY_RESET,
                beforeFree,
                beforePaid,
                wallet.FreeCredits,
                wallet.PaidCredits,
                freeAdded: Math.Max(0, wallet.FreeCredits - beforeFree),
                freeUsed: Math.Max(0, beforeFree - wallet.FreeCredits),
                note: "Daily free credits reset to fixed amount."));
        }

        private async Task<CreditWallet?> GetWalletForUpdateAsync(Guid userId, CancellationToken cancellationToken)
        {
            var hasTransaction = _db.Database.CurrentTransaction != null;
            var provider = _db.Database.ProviderName ?? string.Empty;
            if (hasTransaction && provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({userId.ToString()}, 0))",
                    cancellationToken);

                return await _db.CreditWallets
                    .FromSqlInterpolated($"SELECT * FROM \"CreditWallets\" WHERE \"UserId\" = {userId} FOR UPDATE")
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return await _db.CreditWallets
                .FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);
        }

        private async Task<CreditSettingsDto> GetSettingsAsync(CancellationToken cancellationToken)
        {
            var setting = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            return new CreditSettingsDto(
                setting?.DailyFreeCredits ?? 60,
                Math.Max(1, setting?.CreditTokenUnit ?? 1000),
                Math.Max(1, setting?.CreditOutputTokenWeight ?? 4),
                setting?.EnableCreditSystem ?? true);
        }

        private void DetachPendingWalletCreation(Guid userId)
        {
            var walletEntries = _db.ChangeTracker.Entries<CreditWallet>()
                .Where(e => e.State == EntityState.Added && e.Entity.UserId == userId)
                .ToList();
            foreach (var entry in walletEntries)
            {
                entry.State = EntityState.Detached;
            }

            var initialLedgerEntries = _db.ChangeTracker.Entries<CreditLedger>()
                .Where(e => e.State == EntityState.Added
                    && e.Entity.UserId == userId
                    && e.Entity.Type == CreditLedgerType.DAILY_RESET
                    && e.Entity.Note == "Initial daily free credit reset.")
                .ToList();
            foreach (var entry in initialLedgerEntries)
            {
                entry.State = EntityState.Detached;
            }
        }

        private static bool IsCreditWalletUserUniqueViolation(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                && string.Equals(postgres.ConstraintName, "IX_CreditWallets_UserId", StringComparison.Ordinal);
        }

        private static CreditWallet CreateNewWallet(Guid userId, CreditSettingsDto settings)
        {
            var now = DateTime.UtcNow;
            return new CreditWallet
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FreeCredits = settings.DailyFreeCredits,
                PaidCredits = 0,
                LastFreeCreditResetDate = GetTodayInVietnam(),
                CreatedAt = now,
                UpdatedAt = now,
                Version = 1
            };
        }

        private static CreditLedger CreateLedger(
            Guid userId,
            CreditLedgerType type,
            int beforeFree,
            int beforePaid,
            int afterFree,
            int afterPaid,
            int calculatedCredits = 0,
            int chargedCredits = 0,
            int freeUsed = 0,
            int paidUsed = 0,
            int freeAdded = 0,
            int paidAdded = 0,
            int inputTokens = 0,
            int outputTokens = 0,
            int totalTokens = 0,
            int outputTokenWeight = 0,
            int tokenUnit = 0,
            string? modelName = null,
            bool wasActualTokenUsage = false,
            bool wasInsufficientBalance = false,
            Guid? datasetId = null,
            Guid? chatSessionId = null,
            Guid? chatMessageId = null,
            Guid? relatedPackageId = null,
            Guid? createdByUserId = null,
            string? note = null)
        {
            return new CreditLedger
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DatasetId = datasetId,
                ChatSessionId = chatSessionId,
                ChatMessageId = chatMessageId,
                Type = type,
                CalculatedCredits = Math.Max(0, calculatedCredits),
                ChargedCredits = Math.Max(0, chargedCredits),
                FreeCreditsUsed = Math.Max(0, freeUsed),
                PaidCreditsUsed = Math.Max(0, paidUsed),
                FreeCreditsAdded = Math.Max(0, freeAdded),
                PaidCreditsAdded = Math.Max(0, paidAdded),
                BalanceBeforeFree = Math.Max(0, beforeFree),
                BalanceBeforePaid = Math.Max(0, beforePaid),
                BalanceAfterFree = Math.Max(0, afterFree),
                BalanceAfterPaid = Math.Max(0, afterPaid),
                InputTokens = Math.Max(0, inputTokens),
                OutputTokens = Math.Max(0, outputTokens),
                TotalTokens = Math.Max(0, totalTokens),
                OutputTokenWeight = Math.Max(0, outputTokenWeight),
                TokenUnit = Math.Max(0, tokenUnit),
                ModelName = Truncate(modelName, 120),
                WasActualTokenUsage = wasActualTokenUsage,
                WasInsufficientBalance = wasInsufficientBalance,
                RelatedPackageId = relatedPackageId,
                Note = Truncate(note, 1000),
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTime.UtcNow
            };
        }

        private static int CalculateCredits(int inputTokens, int outputTokens, int tokenUnit, int outputTokenWeight)
        {
            var weightedTokens = Math.Max(0, inputTokens) + Math.Max(0, outputTokens) * Math.Max(1, outputTokenWeight);
            return Math.Max(1, (int)Math.Ceiling(weightedTokens / (double)Math.Max(1, tokenUnit)));
        }

        private static CreditBalanceDto ToBalanceDto(CreditWallet wallet, CreditSettingsDto settings)
        {
            return new CreditBalanceDto(
                wallet.UserId,
                wallet.FreeCredits,
                wallet.PaidCredits,
                wallet.FreeCredits + wallet.PaidCredits,
                wallet.LastFreeCreditResetDate,
                settings);
        }

        private static System.Linq.Expressions.Expression<Func<CreditLedger, CreditLedgerDto>> ToLedgerDtoExpression()
        {
            return l => new CreditLedgerDto(
                l.Id,
                l.UserId,
                l.DatasetId,
                l.ChatSessionId,
                l.ChatMessageId,
                l.Type,
                l.CalculatedCredits,
                l.ChargedCredits,
                l.FreeCreditsUsed,
                l.PaidCreditsUsed,
                l.FreeCreditsAdded,
                l.PaidCreditsAdded,
                l.BalanceAfterFree,
                l.BalanceAfterPaid,
                l.InputTokens,
                l.OutputTokens,
                l.TotalTokens,
                l.ModelName,
                l.WasActualTokenUsage,
                l.WasInsufficientBalance,
                l.Note,
                l.CreatedAt);
        }

        private async Task<IDbContextTransaction?> BeginTransactionIfNeededAsync(CancellationToken cancellationToken)
        {
            return _db.Database.CurrentTransaction == null
                ? await _db.Database.BeginTransactionAsync(cancellationToken)
                : null;
        }

        private static async Task CommitIfOwnedAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken)
        {
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }

        private static DateTime GetTodayInVietnam()
        {
            var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTz);
            return DateTime.SpecifyKind(vietnamNow.Date, DateTimeKind.Utc);
        }

        private static TimeZoneInfo ResolveVietnamTimeZone()
        {
            foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch { }
            }

            return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }
    }
}
