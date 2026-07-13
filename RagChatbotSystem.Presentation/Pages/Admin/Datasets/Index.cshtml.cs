using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Realtime;

namespace RagChatbotSystem.Presentation.Pages.Admin.Datasets
{
    [Authorize(Roles = "Admin")]
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

        [BindProperty]
        public CreateDatasetInput CreateInput { get; set; } = new();

        [BindProperty]
        public EditDatasetInput EditInput { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            await LoadDatasetsAsync(cancellationToken);
        }

        public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
        {
            ModelState.Clear();
            if (!TryValidateModel(CreateInput, nameof(CreateInput)))
            {
                await LoadDatasetsAsync(cancellationToken);
                return Page();
            }

            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Challenge();
            }

            try
            {
                var dataset = await _datasetService.CreateDatasetAsync(
                    new CreateDatasetRequest(CreateInput.Name, CreateInput.Description, currentUserId, CreateInput.IsPublic),
                    cancellationToken);

                SuccessMessage = $"Đã tạo môn học \"{dataset.Name}\".";
                await _realtimeNotifier.DatasetChangedAsync("created", dataset, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create dataset.");
                ErrorMessage = "Không thể tạo môn học. Vui lòng kiểm tra thông tin và thử lại.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync(CancellationToken cancellationToken)
        {
            ModelState.Clear();
            if (!TryValidateModel(EditInput, nameof(EditInput)))
            {
                await LoadDatasetsAsync(cancellationToken);
                return Page();
            }

            try
            {
                var updated = await _datasetService.UpdateDatasetAsync(
                    EditInput.DatasetId,
                    EditInput.Name,
                    EditInput.Description,
                    EditInput.IsPublic,
                    cancellationToken);

                if (!updated)
                {
                    ErrorMessage = "Không tìm thấy môn học.";
                    return RedirectToPage();
                }

                var dataset = await _datasetService.GetDatasetAsync(EditInput.DatasetId, cancellationToken);
                SuccessMessage = "Đã cập nhật môn học.";
                await _realtimeNotifier.DatasetChangedAsync("updated", dataset, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update dataset {DatasetId}.", EditInput.DatasetId);
                ErrorMessage = "Không thể cập nhật môn học. Vui lòng thử lại.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var dataset = await _datasetService.GetDatasetAsync(id, cancellationToken);
                if (dataset == null)
                {
                    ErrorMessage = "Không tìm thấy môn học.";
                    return RedirectToPage();
                }

                if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
                {
                    return Challenge();
                }

                var archived = await _datasetService.ArchiveDatasetAsync(id, archived: true, currentUserId, cancellationToken);
                if (!archived)
                {
                    ErrorMessage = "Không tìm thấy môn học.";
                    return RedirectToPage();
                }

                var archivedDataset = await _datasetService.GetDatasetAsync(id, cancellationToken);
                SuccessMessage = "Đã lưu trữ môn học. Lịch sử trò chuyện, tài liệu và trích dẫn vẫn được giữ lại.";
                await _realtimeNotifier.DatasetChangedAsync("archived", archivedDataset ?? dataset, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to archive dataset {DatasetId}.", id);
                ErrorMessage = "Không thể lưu trữ môn học. Vui lòng thử lại.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRestoreAsync(Guid id, CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            {
                return Challenge();
            }

            var restored = await _datasetService.ArchiveDatasetAsync(id, archived: false, currentUserId, cancellationToken);
            if (!restored)
            {
                ErrorMessage = "Không tìm thấy môn học.";
                return RedirectToPage();
            }

            var dataset = await _datasetService.GetDatasetAsync(id, cancellationToken);
            SuccessMessage = "Đã khôi phục môn học.";
            await _realtimeNotifier.DatasetChangedAsync("restored", dataset, cancellationToken);
            return RedirectToPage();
        }

        private async Task LoadDatasetsAsync(CancellationToken cancellationToken)
        {
            Datasets = await _datasetService.GetDatasetsAsync(cancellationToken: cancellationToken);
        }

        public sealed class CreateDatasetInput
        {
            [Required(ErrorMessage = "Vui lòng nhập tên môn học.")]
            [StringLength(120, ErrorMessage = "Tên môn học không được vượt quá 120 ký tự.")]
            public string Name { get; set; } = string.Empty;

            [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
            public string? Description { get; set; }

            public bool IsPublic { get; set; } = true;
        }

        public sealed class EditDatasetInput
        {
            [Required]
            public Guid DatasetId { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập tên môn học.")]
            [StringLength(120, ErrorMessage = "Tên môn học không được vượt quá 120 ký tự.")]
            public string Name { get; set; } = string.Empty;

            [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
            public string? Description { get; set; }

            public bool IsPublic { get; set; }
        }
    }
}
