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
        private readonly IGenericRepository<User> _userRepository;

        private static readonly TimeZoneInfo VietnamTz = ResolveVietnamTimeZone();

        public StatisticsService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _tokenUsageRepository = _unitOfWork.Repository<UserTokenUsage>();
            _citationRepository = _unitOfWork.Repository<Citation>();
            _userRepository = _unitOfWork.Repository<User>();
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

        public async Task<TokenUsageSummaryDto> GetTokenUsageSummaryAsync(CancellationToken cancellationToken = default)
        {
            var today = GetTodayInVietnam();
            
            var totalTokensUsed = await _tokenUsageRepository.GetQueryable()
                .SumAsync(u => u.TokenCount, cancellationToken);
                
            var totalQueriesCount = await _tokenUsageRepository.GetQueryable()
                .SumAsync(u => u.QueryCount, cancellationToken);

            var todayTokensUsed = await _tokenUsageRepository.GetQueryable()
                .Where(u => u.Date == today)
                .SumAsync(u => u.TokenCount, cancellationToken);

            var activeUsersCount = await _tokenUsageRepository.GetQueryable()
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

        public async Task<List<DailyTokenUsageDto>> GetDailyTokenUsageAsync(int days = 7, CancellationToken cancellationToken = default)
        {
            var cutoffDate = GetTodayInVietnam().AddDays(-days + 1);

            var rawUsage = await _tokenUsageRepository.GetQueryable()
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

        public async Task<List<TopDocumentUsageDto>> GetTopDocumentsUsageAsync(int limit = 5, CancellationToken cancellationToken = default)
        {
            return await _citationRepository.GetQueryable()
                .Include(c => c.Document)
                .ThenInclude(d => d.Dataset)
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

        public async Task<List<UserTokenUsageLeaderboardDto>> GetUserLeaderboardAsync(int limit = 10, CancellationToken cancellationToken = default)
        {
            return await _tokenUsageRepository.GetQueryable()
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
    }
}
