using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Business.Services
{
    public class SystemSettingService : ISystemSettingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<SystemSetting> _repository;

        public SystemSettingService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _repository = _unitOfWork.Repository<SystemSetting>();
        }

        public async Task<int> GetChunkSizeAsync(CancellationToken cancellationToken = default)
        {
            var setting = await _repository.GetQueryable()
                .FirstOrDefaultAsync(cancellationToken);
            return setting?.ChunkSize ?? 500;
        }

        public async Task<int> GetChunkOverlapAsync(CancellationToken cancellationToken = default)
        {
            var setting = await _repository.GetQueryable()
                .FirstOrDefaultAsync(cancellationToken);
            return setting?.ChunkOverlap ?? 100;
        }

        public async Task<int> GetDailyTokenLimitAsync(CancellationToken cancellationToken = default)
        {
            var setting = await _repository.GetQueryable()
                .FirstOrDefaultAsync(cancellationToken);
            return setting?.DailyTokenLimit ?? 50000;
        }

        public async Task UpdateSettingsAsync(int chunkSize, int chunkOverlap, int dailyTokenLimit, CancellationToken cancellationToken = default)
        {
            if (chunkSize < 300 || chunkSize > 700)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "ChunkSize must be between 300 and 700.");

            if (chunkOverlap < 100 || chunkOverlap > chunkSize / 2)
                throw new ArgumentOutOfRangeException(nameof(chunkOverlap), $"ChunkOverlap must be between 100 and {chunkSize / 2}.");

            if (dailyTokenLimit < 1000 || dailyTokenLimit > 10000000)
                throw new ArgumentOutOfRangeException(nameof(dailyTokenLimit), "DailyTokenLimit must be between 1,000 and 10,000,000.");

            var setting = await _repository.GetQueryable()
                .FirstOrDefaultAsync(cancellationToken);

            if (setting == null)
            {
                await _repository.AddAsync(new SystemSetting
                {
                    Id = 1,
                    ChunkSize = chunkSize,
                    ChunkOverlap = chunkOverlap,
                    DailyTokenLimit = dailyTokenLimit,
                    UpdatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
            else
            {
                setting.ChunkSize = chunkSize;
                setting.ChunkOverlap = chunkOverlap;
                setting.DailyTokenLimit = dailyTokenLimit;
                setting.UpdatedAt = DateTime.UtcNow;
                _repository.Update(setting);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
