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

        public async Task UpdateSettingsAsync(int chunkSize, int chunkOverlap, CancellationToken cancellationToken = default)
        {
            if (chunkSize < 300 || chunkSize > 700)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "ChunkSize phải từ 300 đến 700.");

            if (chunkOverlap < 100 || chunkOverlap > chunkSize / 2)
                throw new ArgumentOutOfRangeException(nameof(chunkOverlap), $"ChunkOverlap phải từ 100 đến {chunkSize / 2}.");

            var setting = await _repository.GetQueryable()
                .FirstOrDefaultAsync(cancellationToken);

            if (setting == null)
            {
                await _repository.AddAsync(new SystemSetting
                {
                    Id = 1,
                    ChunkSize = chunkSize,
                    ChunkOverlap = chunkOverlap,
                    UpdatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
            else
            {
                setting.ChunkSize = chunkSize;
                setting.ChunkOverlap = chunkOverlap;
                setting.UpdatedAt = DateTime.UtcNow;
                _repository.Update(setting);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
