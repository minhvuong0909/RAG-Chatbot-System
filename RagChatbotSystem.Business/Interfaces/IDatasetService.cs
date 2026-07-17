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
        Task<IReadOnlyList<DatasetDto>> GetDatasetsForUserAsync(Guid userId, string role, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Guid>> GetAccessibleDatasetIdsAsync(Guid userId, string role, CancellationToken cancellationToken = default);
        Task<DatasetDto?> GetDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default);
        Task<DatasetDto> CreateDatasetAsync(CreateDatasetRequest request, CancellationToken cancellationToken = default);
        Task<bool> UpdateDatasetAsync(Guid datasetId, string name, string? description, bool isPublic, CancellationToken cancellationToken = default);
        Task<bool> ArchiveDatasetAsync(Guid datasetId, bool archived, Guid changedBy, CancellationToken cancellationToken = default);
        Task<bool> ApproveDatasetAsync(Guid datasetId, bool approve, CancellationToken cancellationToken = default);
        Task<bool> GrantPermissionAsync(Guid datasetId, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> RevokePermissionAsync(Guid datasetId, Guid userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserDto>> GetPermittedUsersAsync(Guid datasetId, CancellationToken cancellationToken = default);
        Task<bool> AssignTeacherToDatasetAsync(Guid datasetId, Guid teacherId, Guid adminUserId, CancellationToken cancellationToken = default);
        Task<bool> UnassignTeacherFromDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TeacherSubjectAssignmentDto>> GetTeacherAssignmentsAsync(CancellationToken cancellationToken = default);
        Task<bool> CanManageDatasetAsync(Guid userId, string role, Guid datasetId, CancellationToken cancellationToken = default);
        Task<bool> CanAccessActiveDatasetAsync(Guid userId, string role, Guid datasetId, CancellationToken cancellationToken = default);
    }
}

