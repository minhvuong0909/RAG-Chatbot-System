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

                if (dataset.IsPublic)
                {
                    ErrorMessage = "Public subjects do not require explicit permissions.";
                    return RedirectToPage(new { id });
                }

                if (!string.Equals(user.Role, "Student", StringComparison.Ordinal))
                {
                    return Forbid();
                }

                await _datasetService.GrantPermissionAsync(id, userId, cancellationToken);
                await _realtimeNotifier.DatasetAccessChangedAsync(userId, "granted", dataset, cancellationToken);
                SuccessMessage = $"Access granted to {user.FullName}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to grant dataset {DatasetId} access to user {UserId}.", id, userId);
                ErrorMessage = ex.Message;
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

                var revoked = await _datasetService.RevokePermissionAsync(id, userId, cancellationToken);
                if (!revoked)
                {
                    ErrorMessage = "Permission was not found.";
                    return RedirectToPage(new { id });
                }

                await _realtimeNotifier.DatasetAccessChangedAsync(userId, "revoked", dataset, cancellationToken);
                SuccessMessage = "Permission revoked successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke dataset {DatasetId} access from user {UserId}.", id, userId);
                ErrorMessage = ex.Message;
            }

            return RedirectToPage(new { id });
        }

        private async Task<bool> LoadPageAsync(Guid id, CancellationToken cancellationToken)
        {
            var dataset = await _datasetService.GetDatasetAsync(id, cancellationToken);
            if (dataset == null)
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
