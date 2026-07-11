using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Realtime;

namespace RagChatbotSystem.Presentation.Pages.Datasets
{
    [Authorize(Roles = "Teacher,Admin")]
    public class IndexModel : PageModel
    {
        private readonly IDatasetService _datasetService;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IDatasetService datasetService,
            IRealtimeNotifier realtimeNotifier,
            ILogger<IndexModel> logger)
        {
            _datasetService = datasetService;
            _realtimeNotifier = realtimeNotifier;
            _logger = logger;
        }

        public IReadOnlyList<DatasetDto> Datasets { get; private set; } = Array.Empty<DatasetDto>();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string? error = null, string? success = null)
        {
            if (!TryGetCurrentUser(out var currentUserId, out var role))
            {
                return Challenge();
            }

            ErrorMessage = error;
            SuccessMessage = success;

            try
            {
                Datasets = await _datasetService.GetDatasetsForUserAsync(currentUserId, role, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading datasets list.");
                ErrorMessage = "Không thể tải danh sách môn học. Vui lòng thử lại sau.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCreateDatasetAsync(string name, string? description, bool isPublic)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (!TryGetCurrentUser(out var currentUserId, out _))
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return RedirectToPage("/Datasets/Index", new { error = "Vui lòng nhập tên môn học." });
            }

            try
            {
                var dataset = await _datasetService.CreateDatasetAsync(
                    new CreateDatasetRequest(name, description, currentUserId, isPublic),
                    HttpContext.RequestAborted);

                await _realtimeNotifier.DatasetChangedAsync("created", dataset, HttpContext.RequestAborted);

                return RedirectToPage("/Datasets/Index", new { success = $"Đã tạo môn học \"{dataset.Name}\"." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create dataset.");
                return RedirectToPage("/Datasets/Index", new { error = "Không thể tạo môn học. Vui lòng kiểm tra thông tin và thử lại." });
            }
        }

        public async Task<IActionResult> OnPostDeleteDatasetAsync(Guid id)
        {
            if (!TryGetCurrentUser(out var currentUserId, out var role))
            {
                return Challenge();
            }

            var dataset = await _datasetService.GetDatasetAsync(id, HttpContext.RequestAborted);
            if (dataset == null)
            {
                return RedirectToPage("/Datasets/Index", new { error = "Không tìm thấy môn học." });
            }

            if (role != "Admin" && dataset.CreatedBy != currentUserId)
            {
                return Forbid();
            }

            try
            {
                var archived = await _datasetService.ApproveDatasetAsync(id, approve: false, HttpContext.RequestAborted);
                if (archived)
                {
                    var archivedDataset = await _datasetService.GetDatasetAsync(id, HttpContext.RequestAborted);
                    await _realtimeNotifier.DatasetChangedAsync("unapproved", archivedDataset ?? dataset, HttpContext.RequestAborted);
                }

                return RedirectToPage("/Datasets/Index", new { success = archived ? "Đã ngừng sử dụng môn học. Tài liệu và dữ liệu truy vết vẫn được giữ lại." : "Không tìm thấy môn học." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to archive dataset {DatasetId}.", id);
                return RedirectToPage("/Datasets/Index", new { error = "Không thể ngừng sử dụng môn học. Vui lòng thử lại." });
            }
        }

        private bool TryGetCurrentUser(out Guid userId, out string role)
        {
            role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdString, out userId);
        }
    }
}
