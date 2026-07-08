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

                SuccessMessage = $"Subject '{dataset.Name}' created successfully.";
                await _realtimeNotifier.DatasetChangedAsync("created", dataset, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create dataset.");
                ErrorMessage = ex.Message;
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
                    ErrorMessage = "Subject was not found.";
                    return RedirectToPage();
                }

                var dataset = await _datasetService.GetDatasetAsync(EditInput.DatasetId, cancellationToken);
                SuccessMessage = "Subject updated successfully.";
                await _realtimeNotifier.DatasetChangedAsync("updated", dataset, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update dataset {DatasetId}.", EditInput.DatasetId);
                ErrorMessage = ex.Message;
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
                    ErrorMessage = "Subject was not found.";
                    return RedirectToPage();
                }

                var archived = await _datasetService.ApproveDatasetAsync(id, approve: false, cancellationToken);
                if (!archived)
                {
                    ErrorMessage = "Subject was not found.";
                    return RedirectToPage();
                }

                var archivedDataset = await _datasetService.GetDatasetAsync(id, cancellationToken);
                SuccessMessage = "Subject archived successfully. Existing documents and traces were kept.";
                await _realtimeNotifier.DatasetChangedAsync("unapproved", archivedDataset ?? dataset, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to archive dataset {DatasetId}.", id);
                ErrorMessage = ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostApproveAsync(Guid id, bool approve, CancellationToken cancellationToken)
        {
            try
            {
                var updated = await _datasetService.ApproveDatasetAsync(id, approve, cancellationToken);
                if (!updated)
                {
                    ErrorMessage = "Subject was not found.";
                    return RedirectToPage();
                }

                var dataset = await _datasetService.GetDatasetAsync(id, cancellationToken);
                SuccessMessage = approve ? "Subject approved successfully." : "Subject unapproved successfully.";
                await _realtimeNotifier.DatasetChangedAsync(approve ? "approved" : "unapproved", dataset, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to change approval status for dataset {DatasetId}.", id);
                ErrorMessage = ex.Message;
            }

            return RedirectToPage();
        }

        private async Task LoadDatasetsAsync(CancellationToken cancellationToken)
        {
            Datasets = await _datasetService.GetDatasetsAsync(cancellationToken: cancellationToken);
        }

        public sealed class CreateDatasetInput
        {
            [Required(ErrorMessage = "Subject name is required.")]
            [StringLength(120, ErrorMessage = "Subject name must be 120 characters or fewer.")]
            public string Name { get; set; } = string.Empty;

            [StringLength(500, ErrorMessage = "Description must be 500 characters or fewer.")]
            public string? Description { get; set; }

            public bool IsPublic { get; set; } = true;
        }

        public sealed class EditDatasetInput
        {
            [Required]
            public Guid DatasetId { get; set; }

            [Required(ErrorMessage = "Subject name is required.")]
            [StringLength(120, ErrorMessage = "Subject name must be 120 characters or fewer.")]
            public string Name { get; set; } = string.Empty;

            [StringLength(500, ErrorMessage = "Description must be 500 characters or fewer.")]
            public string? Description { get; set; }

            public bool IsPublic { get; set; }
        }
    }
}
