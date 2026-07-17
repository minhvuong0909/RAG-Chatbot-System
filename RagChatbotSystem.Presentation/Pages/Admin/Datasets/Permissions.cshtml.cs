using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Realtime;

namespace RagChatbotSystem.Presentation.Pages.Admin.Datasets
{
    [Authorize(Roles = "Admin")]
    public class PermissionsModel : PageModel
    {
        private readonly IDatasetService _datasetService;
        private readonly IUserService _userService;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly ILogger<PermissionsModel> _logger;

        public PermissionsModel(
            IDatasetService datasetService,
            IUserService userService,
            IRealtimeNotifier realtimeNotifier,
            ILogger<PermissionsModel> logger)
        {
            _datasetService = datasetService;
            _userService = userService;
            _realtimeNotifier = realtimeNotifier;
            _logger = logger;
        }

        public DatasetDto Dataset { get; private set; } = null!;
        public IReadOnlyList<UserDto> PermittedUsers { get; private set; } = Array.Empty<UserDto>();
        public IReadOnlyList<UserDto> AvailableStudents { get; private set; } = Array.Empty<UserDto>();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
        {
            return await LoadPageAsync(id, cancellationToken) ? Page() : NotFound();
        }

        public async Task<IActionResult> OnPostGrantAsync(Guid id, Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                var dataset = await _datasetService.GetDatasetAsync(id, cancellationToken);
                var user = await _userService.GetUserAsync(userId, cancellationToken);
                if (dataset == null || user == null)
                {
                    return NotFound();
                }

                if (dataset.IsArchived)
                {
                    ErrorMessage = "Môn học đã được lưu trữ và không thể thay đổi quyền truy cập.";
                    return RedirectToPage(new { id });
                }

                if (dataset.IsPublic)
                {
                    ErrorMessage = "Môn học công khai không cần cấp quyền truy cập riêng.";
                    return RedirectToPage(new { id });
                }

                if (!string.Equals(user.Role, "Student", StringComparison.Ordinal))
                {
                    return Forbid();
                }

                await _datasetService.GrantPermissionAsync(id, userId, cancellationToken);
                await _realtimeNotifier.DatasetAccessChangedAsync(userId, "granted", dataset, cancellationToken);
                SuccessMessage = $"Đã cấp quyền truy cập cho {user.FullName}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to grant dataset {DatasetId} access to user {UserId}.", id, userId);
                ErrorMessage = "Không thể cấp quyền truy cập. Vui lòng thử lại.";
            }

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostRevokeAsync(Guid id, Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                var dataset = await _datasetService.GetDatasetAsync(id, cancellationToken);
                if (dataset == null)
                {
                    return NotFound();
                }

                if (dataset.IsArchived)
                {
                    ErrorMessage = "Môn học đã được lưu trữ và không thể thay đổi quyền truy cập.";
                    return RedirectToPage(new { id });
                }

                var revoked = await _datasetService.RevokePermissionAsync(id, userId, cancellationToken);
                if (!revoked)
                {
                    ErrorMessage = "Không tìm thấy quyền truy cập.";
                    return RedirectToPage(new { id });
                }

                await _realtimeNotifier.DatasetAccessChangedAsync(userId, "revoked", dataset, cancellationToken);
                SuccessMessage = "Đã thu hồi quyền truy cập.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke dataset {DatasetId} access from user {UserId}.", id, userId);
                ErrorMessage = "Không thể thu hồi quyền truy cập. Vui lòng thử lại.";
            }

            return RedirectToPage(new { id });
        }

        private async Task<bool> LoadPageAsync(Guid id, CancellationToken cancellationToken)
        {
            var dataset = await _datasetService.GetDatasetAsync(id, cancellationToken);
            if (dataset == null || dataset.IsArchived)
            {
                return false;
            }

            Dataset = dataset;
            PermittedUsers = await _datasetService.GetPermittedUsersAsync(id, cancellationToken);
            var permittedIds = PermittedUsers.Select(user => user.UserId).ToHashSet();
            AvailableStudents = (await _userService.GetUsersAsync(cancellationToken))
                .Where(user => user.Role == "Student" && user.IsApproved && !permittedIds.Contains(user.UserId))
                .ToList();
            return true;
        }
    }
}
