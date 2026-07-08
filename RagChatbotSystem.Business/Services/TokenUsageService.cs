using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Business.Services
{
    public class TokenUsageService : ITokenUsageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<UserTokenUsage> _tokenUsageRepository;
        private readonly IGenericRepository<SystemSetting> _settingRepository;

        private static readonly TimeZoneInfo VietnamTz = ResolveVietnamTimeZone();

        public TokenUsageService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _tokenUsageRepository = _unitOfWork.Repository<UserTokenUsage>();
            _settingRepository = _unitOfWork.Repository<SystemSetting>();
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
            // We want just the date component, but to avoid EF Core/Npgsql timezone conversion issues
            // we specify DateTimeKind.Utc after constructing the date.
            return DateTime.SpecifyKind(vietnamNow.Date, DateTimeKind.Utc);
        }

        public async Task<bool> IsLimitExceededAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var today = GetTodayInVietnam();
            var usages = await _tokenUsageRepository.FindAsync(u => u.UserId == userId && u.Date == today, cancellationToken);
            var usage = usages.Count > 0 ? usages[0] : null;

            if (usage == null)
            {
                return false;
            }

            var settingsList = await _settingRepository.GetAllAsync(cancellationToken);
            var settings = settingsList.Count > 0 ? settingsList[0] : null;
            var limit = settings?.DailyTokenLimit ?? 50000;

            return usage.TokenCount >= limit;
        }

        public async Task RecordUsageAsync(Guid userId, int tokens, CancellationToken cancellationToken = default)
        {
            var today = GetTodayInVietnam();
            var usages = await _tokenUsageRepository.FindAsync(u => u.UserId == userId && u.Date == today, cancellationToken);
            var usage = usages.Count > 0 ? usages[0] : null;

            if (usage == null)
            {
                await _tokenUsageRepository.AddAsync(new UserTokenUsage
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Date = today,
                    TokenCount = tokens,
                    QueryCount = 1
                }, cancellationToken);
            }
            else
            {
                usage.TokenCount += tokens;
                usage.QueryCount += 1;
                _tokenUsageRepository.Update(usage);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> GetDailyUsageAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var today = GetTodayInVietnam();
            var usages = await _tokenUsageRepository.FindAsync(u => u.UserId == userId && u.Date == today, cancellationToken);
            var usage = usages.Count > 0 ? usages[0] : null;
            return usage?.TokenCount ?? 0;
        }
    }
}
