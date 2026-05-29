using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Services
{
    public class DatasetService : IDatasetService
    {
        private readonly AppDbContext _context;

        public DatasetService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(Guid? createdBy = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Datasets.AsNoTracking();

            if (createdBy.HasValue)
            {
                query = query.Where(d => d.CreatedBy == createdBy.Value);
            }

            return await query
                .OrderByDescending(d => d.UpdatedAt)
                .Select(d => new DatasetDto(
                    d.DatasetId,
                    d.Name,
                    d.Description,
                    d.CreatedBy,
                    d.CreatedAt,
                    d.UpdatedAt,
                    d.Documents.Count()))
                .ToListAsync(cancellationToken);
        }

        public async Task<DatasetDto?> GetDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default)
        {
            return await _context.Datasets
                .AsNoTracking()
                .Where(d => d.DatasetId == datasetId)
                .Select(d => new DatasetDto(
                    d.DatasetId,
                    d.Name,
                    d.Description,
                    d.CreatedBy,
                    d.CreatedAt,
                    d.UpdatedAt,
                    d.Documents.Count()))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<DatasetDto> CreateDatasetAsync(CreateDatasetRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Dataset name is required.", nameof(request));
            }

            var creatorExists = await _context.Users.AnyAsync(u => u.UserId == request.CreatedBy, cancellationToken);
            if (!creatorExists)
            {
                throw new InvalidOperationException("Creator user was not found.");
            }

            var now = DateTime.UtcNow;
            var dataset = new Dataset
            {
                DatasetId = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                CreatedBy = request.CreatedBy,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Datasets.Add(dataset);
            await _context.SaveChangesAsync(cancellationToken);

            return ToDto(dataset);
        }

        public async Task<bool> DeleteDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default)
        {
            var dataset = await _context.Datasets.FindAsync(new object[] { datasetId }, cancellationToken);
            if (dataset == null)
            {
                return false;
            }

            _context.Datasets.Remove(dataset);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private static DatasetDto ToDto(Dataset dataset)
        {
            return new DatasetDto(
                dataset.DatasetId,
                dataset.Name,
                dataset.Description,
                dataset.CreatedBy,
                dataset.CreatedAt,
                dataset.UpdatedAt,
                dataset.Documents.Count);
        }
    }
}
