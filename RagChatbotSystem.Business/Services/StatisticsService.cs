using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Business.Services
{
    public class StatisticsService : IStatisticsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<UserTokenUsage> _tokenUsageRepository;
        private readonly IGenericRepository<Citation> _citationRepository;
        private readonly IGenericRepository<CreditLedger> _creditLedgerRepository;
        private readonly IGenericRepository<CreditBlockedAttempt> _blockedAttemptRepository;
        private static readonly TimeZoneInfo VietnamTz = ResolveVietnamTimeZone();

        public StatisticsService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _tokenUsageRepository = _unitOfWork.Repository<UserTokenUsage>();
            _citationRepository = _unitOfWork.Repository<Citation>();
            _creditLedgerRepository = _unitOfWork.Repository<CreditLedger>();
            _blockedAttemptRepository = _unitOfWork.Repository<CreditBlockedAttempt>();
        }

        private static TimeZoneInfo ResolveVietnamTimeZone()
        {
            foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch {}
            }
            return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");
        }

        private DateTime GetTodayInVietnam()
        {
            var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTz);
            return DateTime.SpecifyKind(vietnamNow.Date, DateTimeKind.Utc);
        }

        public async Task<TokenUsageSummaryDto> GetTokenUsageSummaryAsync(IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default)
        {
            var today = GetTodayInVietnam();

            var query = ApplyDatasetScope(_tokenUsageRepository.GetQueryable(), datasetIds);

            var totalTokensUsed = await query
                .SumAsync(u => u.TokenCount, cancellationToken);

            var totalQueriesCount = await query
                .SumAsync(u => u.QueryCount, cancellationToken);

            var todayTokensUsed = await query
                .Where(u => u.Date == today)
                .SumAsync(u => u.TokenCount, cancellationToken);

            var activeUsersCount = await query
                .Select(u => u.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            return new TokenUsageSummaryDto
            {
                TotalTokensUsed = totalTokensUsed,
                TotalQueriesCount = totalQueriesCount,
                TodayTokensUsed = todayTokensUsed,
                ActiveUsersCount = activeUsersCount
            };
        }

        public async Task<List<DailyTokenUsageDto>> GetDailyTokenUsageAsync(int days = 7, IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default)
        {
            var cutoffDate = GetTodayInVietnam().AddDays(-days + 1);

            var rawUsage = await ApplyDatasetScope(_tokenUsageRepository.GetQueryable(), datasetIds)
                .Where(u => u.Date >= cutoffDate)
                .GroupBy(u => u.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    TokenCount = g.Sum(u => u.TokenCount),
                    QueryCount = g.Sum(u => u.QueryCount)
                })
                .OrderBy(g => g.Date)
                .ToListAsync(cancellationToken);

            // Generate full date range to ensure no gaps in the chart
            var result = new List<DailyTokenUsageDto>();
            for (int i = 0; i < days; i++)
            {
                var targetDate = cutoffDate.AddDays(i);
                var match = rawUsage.FirstOrDefault(r => r.Date.Date == targetDate.Date);

                result.Add(new DailyTokenUsageDto
                {
                    Date = targetDate,
                    FormattedDate = targetDate.ToString("dd/MM"),
                    TokenCount = match?.TokenCount ?? 0,
                    QueryCount = match?.QueryCount ?? 0
                });
            }

            return result;
        }

        public async Task<List<TopDocumentUsageDto>> GetTopDocumentsUsageAsync(int limit = 5, IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default)
        {
            var query = _citationRepository.GetQueryable();

            if (datasetIds != null)
            {
                query = query.Where(c => datasetIds.Contains(c.Document.DatasetId));
            }

            return await query
                .GroupBy(c => new { c.DocumentId, c.Document.FileName, DatasetName = c.Document.Dataset.Name })
                .Select(g => new TopDocumentUsageDto
                {
                    DocumentId = g.Key.DocumentId,
                    DocumentName = g.Key.FileName,
                    DatasetName = g.Key.DatasetName,
                    CitationCount = g.Count()
                })
                .OrderByDescending(d => d.CitationCount)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<UserTokenUsageLeaderboardDto>> GetUserLeaderboardAsync(int limit = 10, IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default)
        {
            return await ApplyDatasetScope(_tokenUsageRepository.GetQueryable(), datasetIds)
                .Include(u => u.User)
                .GroupBy(u => new { u.UserId, u.User.FullName, u.User.Email, u.User.Role })
                .Select(g => new UserTokenUsageLeaderboardDto
                {
                    UserId = g.Key.UserId,
                    FullName = g.Key.FullName,
                    Email = g.Key.Email,
                    Role = g.Key.Role,
                    TotalTokensUsed = g.Sum(u => u.TokenCount),
                    TotalQueriesCount = g.Sum(u => u.QueryCount)
                })
                .OrderByDescending(u => u.TotalTokensUsed)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<CreditReportDto> GetCreditReportAsync(int days = 7, IReadOnlyCollection<Guid>? datasetIds = null, CancellationToken cancellationToken = default)
        {
            var cutoffDate = GetTodayInVietnam().AddDays(-days + 1);
            var creditQuery = ApplyDatasetScope(_creditLedgerRepository.GetQueryable(), datasetIds)
                .Where(l => l.Type == CreditLedgerType.SPEND);

            var blockedQuery = _blockedAttemptRepository.GetQueryable()
                .Where(b => b.Reason == CreditBlockedReason.ZERO_BALANCE);
            if (datasetIds != null)
            {
                blockedQuery = blockedQuery.Where(b => b.DatasetId.HasValue && datasetIds.Contains(b.DatasetId.Value));
            }

            var summary = new CreditUsageSummaryDto
            {
                TotalCalculatedCredits = await creditQuery.SumAsync(l => l.CalculatedCredits, cancellationToken),
                TotalChargedCredits = await creditQuery.SumAsync(l => l.ChargedCredits, cancellationToken),
                FreeCreditsConsumed = await creditQuery.SumAsync(l => l.FreeCreditsUsed, cancellationToken),
                PaidCreditsConsumed = await creditQuery.SumAsync(l => l.PaidCreditsUsed, cancellationToken),
                InsufficientBalanceCount = await creditQuery.CountAsync(l => l.WasInsufficientBalance, cancellationToken),
                ZeroBalanceBlockedCount = await blockedQuery.CountAsync(cancellationToken)
            };

            var rawDaily = await creditQuery
                .Where(l => l.CreatedAt >= cutoffDate)
                .GroupBy(l => l.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    CalculatedCredits = g.Sum(l => l.CalculatedCredits),
                    ChargedCredits = g.Sum(l => l.ChargedCredits),
                    FreeCreditsUsed = g.Sum(l => l.FreeCreditsUsed),
                    PaidCreditsUsed = g.Sum(l => l.PaidCreditsUsed)
                })
                .OrderBy(g => g.Date)
                .ToListAsync(cancellationToken);

            var daily = new List<DailyCreditUsageDto>();
            for (var i = 0; i < days; i++)
            {
                var targetDate = cutoffDate.AddDays(i);
                var match = rawDaily.FirstOrDefault(d => d.Date.Date == targetDate.Date);
                daily.Add(new DailyCreditUsageDto
                {
                    Date = targetDate,
                    FormattedDate = targetDate.ToString("dd/MM"),
                    CalculatedCredits = match?.CalculatedCredits ?? 0,
                    ChargedCredits = match?.ChargedCredits ?? 0,
                    FreeCreditsUsed = match?.FreeCreditsUsed ?? 0,
                    PaidCreditsUsed = match?.PaidCreditsUsed ?? 0
                });
            }

            var topStudents = await creditQuery
                .Include(l => l.User)
                .GroupBy(l => new { l.UserId, l.User.FullName, l.User.Email })
                .Select(g => new CreditLeaderboardDto
                {
                    UserId = g.Key.UserId,
                    FullName = g.Key.FullName,
                    Email = g.Key.Email,
                    ChargedCredits = g.Sum(l => l.ChargedCredits),
                    CalculatedCredits = g.Sum(l => l.CalculatedCredits)
                })
                .OrderByDescending(u => u.ChargedCredits)
                .Take(10)
                .ToListAsync(cancellationToken);

            var topDatasets = await creditQuery
                .Where(l => l.DatasetId.HasValue)
                .Include(l => l.Dataset)
                .GroupBy(l => new { DatasetId = l.DatasetId!.Value, DatasetName = l.Dataset!.Name })
                .Select(g => new DatasetCreditUsageDto
                {
                    DatasetId = g.Key.DatasetId,
                    DatasetName = g.Key.DatasetName,
                    ChargedCredits = g.Sum(l => l.ChargedCredits),
                    CalculatedCredits = g.Sum(l => l.CalculatedCredits)
                })
                .OrderByDescending(d => d.ChargedCredits)
                .Take(10)
                .ToListAsync(cancellationToken);

            var topModels = await creditQuery
                .GroupBy(l => l.ModelName ?? "Unknown")
                .Select(g => new ModelCreditUsageDto
                {
                    ModelName = g.Key,
                    ChargedCredits = g.Sum(l => l.ChargedCredits),
                    CalculatedCredits = g.Sum(l => l.CalculatedCredits)
                })
                .OrderByDescending(m => m.ChargedCredits)
                .Take(10)
                .ToListAsync(cancellationToken);

            return new CreditReportDto(summary, daily, topStudents, topDatasets, topModels);
        }

        private static IQueryable<UserTokenUsage> ApplyDatasetScope(
            IQueryable<UserTokenUsage> query,
            IReadOnlyCollection<Guid>? datasetIds)
        {
            return datasetIds != null
                ? query.Where(u => datasetIds.Contains(u.DatasetId))
                : query;
        }

        private static IQueryable<CreditLedger> ApplyDatasetScope(
            IQueryable<CreditLedger> query,
            IReadOnlyCollection<Guid>? datasetIds)
        {
            return datasetIds != null
                ? query.Where(l => l.DatasetId.HasValue && datasetIds.Contains(l.DatasetId.Value))
                : query;
        }
    }
}
