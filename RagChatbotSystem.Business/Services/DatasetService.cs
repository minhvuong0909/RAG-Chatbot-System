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
    public class DatasetService : IDatasetService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<Dataset> _datasetRepository;
        private readonly IGenericRepository<User> _userRepository;

        public DatasetService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _datasetRepository = _unitOfWork.Repository<Dataset>();
            _userRepository = _unitOfWork.Repository<User>();
        }

        public async Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(Guid? createdBy = null, CancellationToken cancellationToken = default)
        {
            var query = _datasetRepository.GetQueryable().AsNoTracking();

            if (createdBy.HasValue)
            {
                query = query.Where(d => d.CreatedBy == createdBy.Value);
            }

            return await MaterializeDatasetDtosAsync(query, cancellationToken);
        }

        public async Task<IReadOnlyList<DatasetDto>> GetDatasetsForUserAsync(Guid userId, string role, CancellationToken cancellationToken = default)
        {
            var query = _datasetRepository.GetQueryable().AsNoTracking();

            if (role == "Teacher")
            {
                query = query.Where(d => d.TeacherSubjectAssignment != null && d.TeacherSubjectAssignment.TeacherId == userId);
            }
            else if (role != "Admin")
            {
                query = query.Where(d =>
                    (d.IsPublic && d.IsApproved) ||
                    d.DatasetPermissions.Any(dp => dp.UserId == userId));
            }

            return await MaterializeDatasetDtosAsync(query, cancellationToken);
        }

        public async Task<DatasetDto?> GetDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default)
        {
            var dataset = await IncludeDatasetSummary(_datasetRepository.GetQueryable().AsNoTracking())
                .FirstOrDefaultAsync(d => d.DatasetId == datasetId, cancellationToken);

            return dataset == null ? null : ToDto(dataset);
        }

        public async Task<DatasetDto> CreateDatasetAsync(CreateDatasetRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Subject name is required.", nameof(request));
            }

            var creatorExists = await _userRepository.GetQueryable().AnyAsync(u => u.UserId == request.CreatedBy, cancellationToken);
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
                UpdatedAt = now,
                IsPublic = request.IsPublic,
                IsApproved = true
            };

            await _datasetRepository.AddAsync(dataset, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ToDto(dataset);
        }

        public async Task<bool> UpdateDatasetAsync(Guid datasetId, string name, string? description, bool isPublic, CancellationToken cancellationToken = default)
        {
            var dataset = await _datasetRepository.GetByIdAsync(datasetId, cancellationToken);
            if (dataset == null)
            {
                return false;
            }

            dataset.Name = name.Trim();
            dataset.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            dataset.IsPublic = isPublic;
            dataset.UpdatedAt = DateTime.UtcNow;

            _datasetRepository.Update(dataset);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> DeleteDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default)
        {
            var dataset = await _datasetRepository.GetByIdAsync(datasetId, cancellationToken);
            if (dataset == null)
            {
                return false;
            }

            _datasetRepository.Delete(dataset);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> ApproveDatasetAsync(Guid datasetId, bool approve, CancellationToken cancellationToken = default)
        {
            var dataset = await _datasetRepository.GetByIdAsync(datasetId, cancellationToken);
            if (dataset == null)
            {
                return false;
            }

            dataset.IsApproved = approve;
            dataset.UpdatedAt = DateTime.UtcNow;

            _datasetRepository.Update(dataset);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> GrantPermissionAsync(Guid datasetId, Guid userId, CancellationToken cancellationToken = default)
        {
            var permissionRepo = _unitOfWork.Repository<DatasetPermission>();
            var exists = await permissionRepo.GetQueryable()
                .AnyAsync(dp => dp.DatasetId == datasetId && dp.UserId == userId, cancellationToken);

            if (!exists)
            {
                await permissionRepo.AddAsync(new DatasetPermission
                {
                    PermissionId = Guid.NewGuid(),
                    DatasetId = datasetId,
                    UserId = userId,
                    GrantedAt = DateTime.UtcNow
                }, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return true;
        }

        public async Task<bool> RevokePermissionAsync(Guid datasetId, Guid userId, CancellationToken cancellationToken = default)
        {
            var permissionRepo = _unitOfWork.Repository<DatasetPermission>();
            var permission = await permissionRepo.GetQueryable()
                .FirstOrDefaultAsync(dp => dp.DatasetId == datasetId && dp.UserId == userId, cancellationToken);

            if (permission == null)
            {
                return false;
            }

            permissionRepo.Delete(permission);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<IReadOnlyList<UserDto>> GetPermittedUsersAsync(Guid datasetId, CancellationToken cancellationToken = default)
        {
            var permissionRepo = _unitOfWork.Repository<DatasetPermission>();
            return await permissionRepo.GetQueryable()
                .AsNoTracking()
                .Where(dp => dp.DatasetId == datasetId)
                .OrderBy(dp => dp.User.FullName)
                .Select(dp => new UserDto(
                    dp.User.UserId,
                    dp.User.FullName,
                    dp.User.Email,
                    dp.User.Username,
                    dp.User.Role,
                    dp.User.CreatedAt,
                    dp.User.IsApproved,
                    dp.User.MustChangePassword))
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> AssignTeacherToDatasetAsync(Guid datasetId, Guid teacherId, Guid adminUserId, CancellationToken cancellationToken = default)
        {
            var datasetExists = await _datasetRepository.GetQueryable().AnyAsync(d => d.DatasetId == datasetId, cancellationToken);
            if (!datasetExists)
            {
                throw new InvalidOperationException("Subject was not found.");
            }

            var teacher = await _userRepository.GetQueryable().FirstOrDefaultAsync(u => u.UserId == teacherId, cancellationToken);
            if (teacher == null || teacher.Role != "Teacher")
            {
                throw new InvalidOperationException("Teacher account was not found.");
            }

            var assignmentRepo = _unitOfWork.Repository<TeacherSubjectAssignment>();
            var existingForDataset = await assignmentRepo.GetQueryable()
                .FirstOrDefaultAsync(a => a.DatasetId == datasetId, cancellationToken);

            if (existingForDataset != null)
            {
                if (existingForDataset.TeacherId == teacherId)
                {
                    return true;
                }

                throw new InvalidOperationException("This subject is already assigned to another teacher.");
            }

            await assignmentRepo.AddAsync(new TeacherSubjectAssignment
            {
                AssignmentId = Guid.NewGuid(),
                DatasetId = datasetId,
                TeacherId = teacherId,
                AssignedBy = adminUserId,
                AssignedAt = DateTime.UtcNow
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> UnassignTeacherFromDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default)
        {
            var assignmentRepo = _unitOfWork.Repository<TeacherSubjectAssignment>();
            var assignment = await assignmentRepo.GetQueryable()
                .FirstOrDefaultAsync(a => a.DatasetId == datasetId, cancellationToken);

            if (assignment == null)
            {
                return false;
            }

            assignmentRepo.Delete(assignment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<IReadOnlyList<TeacherSubjectAssignmentDto>> GetTeacherAssignmentsAsync(CancellationToken cancellationToken = default)
        {
            var assignmentRepo = _unitOfWork.Repository<TeacherSubjectAssignment>();
            return await assignmentRepo.GetQueryable()
                .AsNoTracking()
                .OrderBy(a => a.Teacher.FullName)
                .ThenBy(a => a.Dataset.Name)
                .Select(a => new TeacherSubjectAssignmentDto(
                    a.AssignmentId,
                    a.TeacherId,
                    a.Teacher.FullName,
                    a.Teacher.Email,
                    a.DatasetId,
                    a.Dataset.Name,
                    a.AssignedBy,
                    a.AssignedAt))
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> CanManageDatasetAsync(Guid userId, string role, Guid datasetId, CancellationToken cancellationToken = default)
        {
            if (role == "Admin")
            {
                return true;
            }

            if (role != "Teacher")
            {
                return false;
            }

            var assignmentRepo = _unitOfWork.Repository<TeacherSubjectAssignment>();
            return await assignmentRepo.GetQueryable()
                .AnyAsync(a => a.DatasetId == datasetId && a.TeacherId == userId, cancellationToken);
        }

        private static async Task<IReadOnlyList<DatasetDto>> MaterializeDatasetDtosAsync(IQueryable<Dataset> query, CancellationToken cancellationToken)
        {
            var datasets = await IncludeDatasetSummary(query)
                .OrderByDescending(d => d.UpdatedAt)
                .ToListAsync(cancellationToken);

            return datasets.Select(ToDto).ToList();
        }

        private static IQueryable<Dataset> IncludeDatasetSummary(IQueryable<Dataset> query)
        {
            return query
                .Include(d => d.Documents)
                .Include(d => d.TeacherSubjectAssignment)
                    .ThenInclude(a => a!.Teacher)
                .AsSplitQuery();
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
                dataset.Documents.Count,
                dataset.IsPublic,
                dataset.IsApproved,
                dataset.TeacherSubjectAssignment?.TeacherId,
                dataset.TeacherSubjectAssignment?.Teacher?.FullName);
        }
    }
}
