using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Services;

namespace RagChatbotSystem.Presentation.Pages.Admin.ModelComparison
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly IModelComparisonService _modelComparisonService;
        private readonly IDatasetService _datasetService;
        private readonly BatchComparisonService _batchComparisonService;

        public IndexModel(
            IModelComparisonService modelComparisonService, 
            IDatasetService datasetService,
            BatchComparisonService batchComparisonService)
        {
            _modelComparisonService = modelComparisonService;
            _datasetService = datasetService;
            _batchComparisonService = batchComparisonService;
        }

        // Giới hạn số câu cho 1 lần chạy hàng loạt — chặn lỡ tay dán quá nhiều làm cháy quota free-tier.
        // Khai báo 1 chỗ để cả server (validate) lẫn view (cảnh báo) dùng chung, không lệch số.
        public const int MaxBatchQuestions = 20;

        [BindProperty]
        public ComparisonInput Input { get; set; } = new();

        public IReadOnlyList<DatasetDto> Datasets { get; private set; } = Array.Empty<DatasetDto>();
        public IReadOnlyList<string> AvailableProviders => _modelComparisonService.AvailableProviders;
        public ModelComparisonRunResultDto? Result { get; private set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public BatchJobStatus BatchStatus { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!TryGetCurrentUser(out var userId, out var role))
            {
                return Challenge();
            }

            // Nhận thông báo do luồng POST (batch) chuyển sang sau khi redirect (PRG).
            SuccessMessage = TempData["SuccessMessage"] as string;
            ErrorMessage = TempData["ErrorMessage"] as string;
            Datasets = await _datasetService.GetDatasetsForUserAsync(userId, role);
            BatchStatus = _batchComparisonService.GetStatus();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!TryGetCurrentUser(out var userId, out var role))
            {
                return Challenge();
            }

            Datasets = await _datasetService.GetDatasetsForUserAsync(userId, role);
            BatchStatus = _batchComparisonService.GetStatus();

            if (BatchStatus.IsRunning)
            {
                ErrorMessage = "Đang có một tiến trình benchmark chạy ngầm. Vui lòng đợi hoàn thành.";
                return Page();
            }

            if (Input.Providers == null || Input.Providers.Count == 0)
            {
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Providers)}", "Vui lòng chọn ít nhất 1 model để thử nghiệm.");
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Vui lòng kiểm tra và sửa các trường chưa hợp lệ.";
                return Page();
            }

            // Phân tách câu hỏi theo dòng để kiểm tra xem có chạy batch hay không
            var lines = (Input.Question ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            if (lines.Count == 0)
            {
                ErrorMessage = "Vui lòng nhập câu hỏi.";
                return Page();
            }

            if (lines.Count > MaxBatchQuestions)
            {
                ErrorMessage = $"Bạn nhập {lines.Count} câu, tối đa {MaxBatchQuestions} câu mỗi lần chạy hàng loạt. Vui lòng giảm bớt rồi thử lại.";
                return Page();
            }

            if (lines.Count > 1)
            {
                // Chạy ngầm hàng loạt (Batch Benchmark)
                var started = _batchComparisonService.StartBatch(Input.DatasetId, lines, Input.Providers ?? new List<string>(), userId);
                if (started)
                {
                    TempData["SuccessMessage"] = $"Đã khởi chạy tiến trình Benchmark ngầm cho {lines.Count} câu hỏi thành công. Bạn có thể theo dõi tiến độ bên dưới.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không thể bắt đầu tiến trình chạy ngầm. Có thể đang có một tiến trình khác đang chạy.";
                }
                // PRG: redirect để trang thành GET — tránh reload/F5 gửi lại POST và vô tình chạy batch lần nữa.
                return RedirectToPage();
            }

            // Chạy đơn câu hỏi (Single Question) đồng bộ như cũ
            try
            {
                Result = await _modelComparisonService.CompareAsync(Input.DatasetId, lines[0], Input.Providers ?? new List<string>(), userId);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Không thể chạy thử nghiệm: {ex.Message}";
            }

            return Page();
        }

        public IActionResult OnGetStatus()
        {
            return new JsonResult(_batchComparisonService.GetStatus());
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
            [StringLength(5000, ErrorMessage = "Câu hỏi tối đa 5000 ký tự.")]
            public string Question { get; set; } = string.Empty;

            public List<string> Providers { get; set; } = new();
        }
    }
}
