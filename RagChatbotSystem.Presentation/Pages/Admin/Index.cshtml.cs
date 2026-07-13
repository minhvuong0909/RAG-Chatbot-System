using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Realtime;

namespace RagChatbotSystem.Presentation.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private const long MaxImportFileSize = 5 * 1024 * 1024;

        private readonly IUserService _userService;
        private readonly IDatasetService _datasetService;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly IConfiguration _configuration;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IUserService userService,
            IDatasetService datasetService,
            IRealtimeNotifier realtimeNotifier,
            IConfiguration configuration,
            ILogger<IndexModel> logger)
        {
            _userService = userService;
            _datasetService = datasetService;
            _realtimeNotifier = realtimeNotifier;
            _configuration = configuration;
            _logger = logger;
        }

        public IReadOnlyList<UserDto> Users { get; private set; } = Array.Empty<UserDto>();
        public IReadOnlyList<UserDto> ApprovedTeachers { get; private set; } = Array.Empty<UserDto>();
        public IReadOnlyList<DatasetDto> UnassignedDatasets { get; private set; } = Array.Empty<DatasetDto>();
        public IReadOnlyList<TeacherSubjectAssignmentDto> Assignments { get; private set; } = Array.Empty<TeacherSubjectAssignmentDto>();

        [BindProperty]
        public CreateTeacherInput TeacherInput { get; set; } = new();

        [BindProperty]
        public AssignTeacherInput AssignmentInput { get; set; } = new();

        [BindProperty]
        public IFormFile? StudentsFile { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        [TempData]
        public string? ImportErrors { get; set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            await LoadDashboardAsync(cancellationToken);
        }

        public async Task<IActionResult> OnPostImportStudentsAsync(CancellationToken cancellationToken)
        {
            if (StudentsFile == null || StudentsFile.Length == 0)
            {
                ErrorMessage = "Vui lòng chọn tệp XLSX để nhập danh sách sinh viên.";
                return RedirectToPage();
            }

            if (!string.Equals(Path.GetExtension(StudentsFile.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Hệ thống chỉ hỗ trợ tệp XLSX.";
                return RedirectToPage();
            }

            if (StudentsFile.Length > MaxImportFileSize)
            {
                ErrorMessage = "Tệp XLSX không được vượt quá 5 MB.";
                return RedirectToPage();
            }

            if (!TryGetCurrentUserId(out var adminUserId))
            {
                return Challenge();
            }

            try
            {
                if (!IsSmtpConfigured())
                {
                    ErrorMessage = "Email SMTP chưa được cấu hình. Vui lòng cấu hình SMTP trước khi nhập sinh viên.";
                    return RedirectToPage();
                }

                await using var stream = StudentsFile.OpenReadStream();
                var result = await _userService.ImportStudentsFromXlsxAsync(stream, adminUserId, cancellationToken);
                SuccessMessage = $"Đã nhập sinh viên: tạo thành công {result.CreatedCount}/{result.TotalRows} tài khoản, thất bại {result.FailedCount}.";
                ImportErrors = string.Join(" | ", result.Rows
                    .Where(row => !row.Success)
                    .Take(8)
                    .Select(row => $"Dòng {row.RowNumber}: {row.ErrorMessage}"));

                await _realtimeNotifier.AdminChangedAsync("students-imported", SuccessMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import student accounts.");
                ErrorMessage = $"Không thể nhập danh sách sinh viên: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCreateTeacherAsync(CancellationToken cancellationToken)
        {
            ModelState.Clear();
            if (!TryValidateModel(TeacherInput, nameof(TeacherInput)))
            {
                await LoadDashboardAsync(cancellationToken);
                return Page();
            }

            if (!TryGetCurrentUserId(out var adminUserId))
            {
                return Challenge();
            }

            try
            {
                if (!IsSmtpConfigured())
                {
                    ErrorMessage = "Email SMTP chưa được cấu hình. Vui lòng cấu hình SMTP trước khi tạo tài khoản giảng viên.";
                    return RedirectToPage();
                }

                var provisioned = await _userService.CreateTeacherByAdminAsync(
                    new AdminCreateTeacherRequest(TeacherInput.FullName, TeacherInput.Email, TeacherInput.DatasetIds),
                    adminUserId,
                    cancellationToken);

                SuccessMessage = $"Đã tạo tài khoản giảng viên và gửi tới {provisioned.Email}. Tên đăng nhập: {provisioned.Username}.";
                await _realtimeNotifier.AdminChangedAsync("teacher-created", SuccessMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create teacher account for {Email}.", TeacherInput.Email);
                ErrorMessage = $"Không thể tạo tài khoản giảng viên: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAssignTeacherAsync(CancellationToken cancellationToken)
        {
            ModelState.Clear();
            if (!TryValidateModel(AssignmentInput, nameof(AssignmentInput)))
            {
                await LoadDashboardAsync(cancellationToken);
                return Page();
            }

            if (!TryGetCurrentUserId(out var adminUserId))
            {
                return Challenge();
            }

            try
            {
                await _datasetService.AssignTeacherToDatasetAsync(
                    AssignmentInput.DatasetId,
                    AssignmentInput.TeacherId,
                    adminUserId,
                    cancellationToken);

                var dataset = await _datasetService.GetDatasetAsync(AssignmentInput.DatasetId, cancellationToken);
                if (dataset != null)
                {
                    await _realtimeNotifier.DatasetAccessChangedAsync(
                        AssignmentInput.TeacherId,
                        "assigned",
                        dataset,
                        cancellationToken);
                }

                SuccessMessage = "Đã phân công môn học cho giảng viên.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign teacher {TeacherId} to dataset {DatasetId}.", AssignmentInput.TeacherId, AssignmentInput.DatasetId);
                ErrorMessage = ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUnassignTeacherAsync(Guid datasetId, CancellationToken cancellationToken)
        {
            try
            {
                var assignment = (await _datasetService.GetTeacherAssignmentsAsync(cancellationToken))
                    .FirstOrDefault(item => item.DatasetId == datasetId);
                var dataset = await _datasetService.GetDatasetAsync(datasetId, cancellationToken);
                var removed = await _datasetService.UnassignTeacherFromDatasetAsync(datasetId, cancellationToken);

                if (!removed)
                {
                    ErrorMessage = "Không tìm thấy phân công giảng viên cần gỡ.";
                    return RedirectToPage();
                }

                if (assignment != null && dataset != null)
                {
                    await _realtimeNotifier.DatasetAccessChangedAsync(
                        assignment.TeacherId,
                        "unassigned",
                        dataset,
                        cancellationToken);
                }

                SuccessMessage = "Đã gỡ phân công giảng viên khỏi môn học.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove teacher assignment for dataset {DatasetId}.", datasetId);
                ErrorMessage = ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostApproveUserAsync(Guid userId, bool approve, CancellationToken cancellationToken)
        {
            var user = await _userService.GetUserAsync(userId, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            if (!string.Equals(user.Role, "Teacher", StringComparison.Ordinal))
            {
                return Forbid();
            }

            var updated = await _userService.ApproveUserAsync(userId, approve, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }

            await _realtimeNotifier.UserApprovalChangedAsync(userId, approve, cancellationToken);
            SuccessMessage = approve ? "Đã kích hoạt tài khoản giảng viên." : "Đã khóa tài khoản giảng viên.";
            return RedirectToPage();
        }

        private async Task LoadDashboardAsync(CancellationToken cancellationToken)
        {
            Users = await _userService.GetUsersAsync(cancellationToken);
            var datasets = await _datasetService.GetDatasetsAsync(cancellationToken: cancellationToken);
            Assignments = await _datasetService.GetTeacherAssignmentsAsync(cancellationToken);
            ApprovedTeachers = Users.Where(user => user.Role == "Teacher" && user.IsApproved).ToList();
            UnassignedDatasets = datasets.Where(dataset => !dataset.IsArchived && dataset.AssignedTeacherId == null).ToList();
        }

        private bool TryGetCurrentUserId(out Guid userId)
        {
            return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }

        private bool IsSmtpConfigured()
        {
            return !string.IsNullOrWhiteSpace(_configuration["Smtp:Host"]) &&
                   !string.IsNullOrWhiteSpace(_configuration["Smtp:Username"]) &&
                   !string.IsNullOrWhiteSpace(_configuration["Smtp:Password"]) &&
                   !string.IsNullOrWhiteSpace(_configuration["Smtp:FromEmail"]);
        }

        public sealed class CreateTeacherInput
        {
            [Required(ErrorMessage = "Vui lòng nhập họ tên giảng viên.")]
            [StringLength(120)]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập email giảng viên.")]
            [EmailAddress(ErrorMessage = "Email giảng viên không hợp lệ.")]
            [StringLength(256)]
            public string Email { get; set; } = string.Empty;

            [MinLength(1, ErrorMessage = "Vui lòng chọn ít nhất một môn học.")]
            public List<Guid> DatasetIds { get; set; } = new();
        }

        public sealed class AssignTeacherInput
        {
            [Required]
            public Guid TeacherId { get; set; }

            [Required]
            public Guid DatasetId { get; set; }
        }
    }
}
