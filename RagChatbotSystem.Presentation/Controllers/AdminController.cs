using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IUserService _userService;
        private readonly IDatasetService _datasetService;
        private readonly IConfiguration _configuration;

        public AdminController(IUserService userService, IDatasetService datasetService, IConfiguration configuration)
        {
            _userService = userService;
            _datasetService = datasetService;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            await PopulateDashboardDataAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportStudents(IFormFile studentsFile)
        {
            if (studentsFile == null || studentsFile.Length == 0)
            {
                TempData["AdminError"] = "Please choose an XLSX file to import students.";
                return RedirectToAction(nameof(Index));
            }

            var adminUserId = GetCurrentUserId();
            try
            {
                if (!IsSmtpConfigured())
                {
                    TempData["AdminError"] = "SMTP email is not configured. Configure Smtp settings before importing students.";
                    return RedirectToAction(nameof(Index));
                }

                await using var stream = studentsFile.OpenReadStream();
                var result = await _userService.ImportStudentsFromXlsxAsync(stream, adminUserId);
                TempData["AdminSuccess"] = $"Student import completed. Created {result.CreatedCount}/{result.TotalRows} accounts. Failed {result.FailedCount}.";
                TempData["ImportErrors"] = string.Join(" | ", result.Rows.Where(r => !r.Success).Take(8).Select(r => $"Row {r.RowNumber}: {r.ErrorMessage}"));
            }
            catch (Exception ex)
            {
                TempData["AdminError"] = $"Student import failed: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeacher(string fullName, string email, Guid[] datasetIds)
        {
            var adminUserId = GetCurrentUserId();
            try
            {
                if (!IsSmtpConfigured())
                {
                    TempData["AdminError"] = "SMTP email is not configured. Configure Smtp settings before creating teacher accounts.";
                    return RedirectToAction(nameof(Index));
                }

                var provisioned = await _userService.CreateTeacherByAdminAsync(
                    new AdminCreateTeacherRequest(fullName, email, datasetIds),
                    adminUserId);

                TempData["AdminSuccess"] = $"Teacher account created and emailed to {provisioned.Email}. Username: {provisioned.Username}.";
            }
            catch (Exception ex)
            {
                TempData["AdminError"] = $"Teacher creation failed: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveDataset(Guid id, bool approve)
        {
            var success = await _datasetService.ApproveDatasetAsync(id, approve);
            if (!success)
            {
                return NotFound();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Permissions(Guid id)
        {
            var dataset = await _datasetService.GetDatasetAsync(id);
            if (dataset == null)
            {
                return NotFound();
            }

            var permittedUsers = await _datasetService.GetPermittedUsersAsync(id);
            var allUsers = await _userService.GetUsersAsync();

            var availableUsers = allUsers
                .Where(u => u.Role != "Admin" && u.UserId != dataset.CreatedBy && !permittedUsers.Any(pu => pu.UserId == u.UserId))
                .ToList();

            ViewBag.Dataset = dataset;
            ViewBag.PermittedUsers = permittedUsers;
            ViewBag.AvailableUsers = availableUsers;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantPermission(Guid datasetId, Guid userId)
        {
            await _datasetService.GrantPermissionAsync(datasetId, userId);
            return RedirectToAction(nameof(Permissions), new { id = datasetId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokePermission(Guid datasetId, Guid userId)
        {
            await _datasetService.RevokePermissionAsync(datasetId, userId);
            return RedirectToAction(nameof(Permissions), new { id = datasetId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTeacher(Guid datasetId, Guid teacherId)
        {
            try
            {
                await _datasetService.AssignTeacherToDatasetAsync(datasetId, teacherId, GetCurrentUserId());
                TempData["AdminSuccess"] = "Teacher assigned to subject successfully.";
            }
            catch (Exception ex)
            {
                TempData["AdminError"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnassignTeacher(Guid datasetId)
        {
            await _datasetService.UnassignTeacherFromDatasetAsync(datasetId);
            TempData["AdminSuccess"] = "Teacher assignment removed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveUser(Guid userId, bool approve)
        {
            var success = await _userService.ApproveUserAsync(userId, approve);
            if (!success)
            {
                return NotFound();
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDashboardDataAsync()
        {
            var users = await _userService.GetUsersAsync();
            var datasets = await _datasetService.GetDatasetsAsync();
            var assignments = await _datasetService.GetTeacherAssignmentsAsync();

            ViewBag.Users = users;
            ViewBag.Teachers = users.Where(u => u.Role == "Teacher" && u.IsApproved).ToList();
            ViewBag.Datasets = datasets;
            ViewBag.Assignments = assignments;
            ViewBag.UnassignedDatasets = datasets.Where(d => d.AssignedTeacherId == null).ToList();
        }

        private Guid GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(value, out var userId))
            {
                throw new InvalidOperationException("Admin session is invalid.");
            }

            return userId;
        }

        private bool IsSmtpConfigured()
        {
            return !string.IsNullOrWhiteSpace(_configuration["Smtp:Host"]) &&
                   !string.IsNullOrWhiteSpace(_configuration["Smtp:Username"]) &&
                   !string.IsNullOrWhiteSpace(_configuration["Smtp:Password"]) &&
                   !string.IsNullOrWhiteSpace(_configuration["Smtp:FromEmail"]);
        }
    }
}
