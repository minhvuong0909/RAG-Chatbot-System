using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IDatasetService
    {
        Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(Guid? createdBy = null, CancellationToken cancellationToken = default);
        Task<DatasetDto?> GetDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default);
        Task<DatasetDto> CreateDatasetAsync(CreateDatasetRequest request, CancellationToken cancellationToken = default);
        Task<bool> DeleteDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default);
    }
}
