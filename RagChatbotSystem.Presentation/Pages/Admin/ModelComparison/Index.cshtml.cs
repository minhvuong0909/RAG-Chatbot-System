using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Pages.Admin.ModelComparison
{
    [Authorize(Roles = "Admin,Teacher")]
    public class IndexModel : PageModel
    {
        private readonly IModelComparisonService _modelComparisonService;
        private readonly IDatasetService _datasetService;

        public IndexModel(IModelComparisonService modelComparisonService, IDatasetService datasetService)
        {
            _modelComparisonService = modelComparisonService;
            _datasetService = datasetService;
        }

        [BindProperty]
        public ComparisonInput Input { get; set; } = new();

        public IReadOnlyList<DatasetDto> Datasets { get; private set; } = Array.Empty<DatasetDto>();
        public IReadOnlyList<string> AvailableProviders => _modelComparisonService.AvailableProviders;
        public ModelComparisonRunResultDto? Result { get; private set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!TryGetCurrentUser(out var userId, out var role))
            {
                return Challenge();
            }

            Datasets = await _datasetService.GetDatasetsForUserAsync(userId, role);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!TryGetCurrentUser(out var userId, out var role))
            {
                return Challenge();
            }

            Datasets = await _datasetService.GetDatasetsForUserAsync(userId, role);

            if (Input.Providers == null || Input.Providers.Count == 0)
            {
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Providers)}", "Vui lòng chọn ít nhất 1 model để thử nghiệm.");
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Vui lòng kiểm tra và sửa các trường chưa hợp lệ.";
                return Page();
            }

            try
            {
                Result = await _modelComparisonService.CompareAsync(Input.DatasetId, Input.Question.Trim(), Input.Providers ?? new List<string>(), userId);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Không thể chạy thử nghiệm: {ex.Message}";
            }

            return Page();
        }

        private bool TryGetCurrentUser(out Guid userId, out string role)
        {
            role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdString, out userId);
        }

        public class ComparisonInput
        {
            [Required(ErrorMessage = "Vui lòng chọn môn học.")]
            public Guid DatasetId { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập câu hỏi thử nghiệm.")]
            [StringLength(1000, ErrorMessage = "Câu hỏi tối đa 1000 ký tự.")]
            public string Question { get; set; } = string.Empty;

            public List<string> Providers { get; set; } = new();
        }
    }
}
