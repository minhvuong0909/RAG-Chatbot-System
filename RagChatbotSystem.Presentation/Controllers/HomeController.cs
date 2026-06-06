using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Models;

namespace RagChatbotSystem.Presentation.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IUserService _userService;
        private readonly IDatasetService _datasetService;
        private readonly IDocumentService _documentService;
        private readonly IChatSessionService _chatSessionService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IUserService userService,
            IDatasetService datasetService,
            IDocumentService documentService,
            IChatSessionService chatSessionService,
            ILogger<HomeController> logger)
        {
            _userService = userService;
            _datasetService = datasetService;
            _documentService = documentService;
            _chatSessionService = chatSessionService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(Guid? datasetId = null, Guid? sessionId = null, Guid? citationId = null, string? error = null, string? success = null)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var currentUserRole = User.FindFirstValue(ClaimTypes.Role) ?? "Student";
            if (currentUserRole == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }

            if (string.Equals(User.FindFirstValue("MustChangePassword"), "True", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("ChangePassword", "Account");
            }

            var model = new WorkspaceViewModel
            {
                SelectedUserId = currentUserId,
                SelectedDatasetId = datasetId,
                SelectedSessionId = sessionId,
                SelectedMessageId = citationId,
                ErrorMessage = error,
                SuccessMessage = success
            };

            try
            {
                // 1. Lấy thông tin User hiện tại
                model.SelectedUser = await _userService.GetUserAsync(currentUserId);
                if (model.SelectedUser == null)
                {
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    TempData["ErrorMessage"] = "Your login session is no longer valid. Please sign in again.";
                    return RedirectToAction("Login", "Account");
                }

                // 2. Lấy danh sách Datasets phù hợp với phân quyền của User
                model.Datasets = await _datasetService.GetDatasetsForUserAsync(currentUserId, currentUserRole);

                // 3. Nếu có Dataset được chọn, lấy thông tin Dataset, danh sách tài liệu và danh sách Sessions
                if (datasetId.HasValue)
                {
                    model.SelectedDataset = await _datasetService.GetDatasetAsync(datasetId.Value);
                    if (model.SelectedDataset != null)
                    {
                        // Kiểm tra xem User hiện tại có quyền truy cập Dataset này không
                        var hasAccess = currentUserRole == "Admin" ||
                                        model.Datasets.Any(d => d.DatasetId == datasetId.Value);

                        if (!hasAccess)
                        {
                            return RedirectToAction("Index", new { error = "You do not have permission to access this dataset." });
                        }

                        model.Documents = await _documentService.GetDocumentsByDatasetAsync(datasetId.Value);
                        model.Sessions = await _chatSessionService.GetSessionsAsync(currentUserId, datasetId.Value);
                    }
                }

                // 4. Nếu có Session được chọn, lấy thông tin Session và lịch sử chat
                if (sessionId.HasValue)
                {
                    model.SelectedSession = await _chatSessionService.GetSessionAsync(sessionId.Value);
                    if (model.SelectedSession != null)
                    {
                        // Bảo mật: Đảm bảo học sinh chỉ xem được session của chính mình
                        if (currentUserRole != "Admin" && model.SelectedSession.UserId != currentUserId)
                        {
                            return RedirectToAction("Index", new { datasetId, error = "Access denied to this chat session." });
                        }

                        model.MessageHistory = await _chatSessionService.GetMessageHistoryAsync(sessionId.Value);
                    }
                }

                // 5. Nếu có yêu cầu xem nguồn trích dẫn
                if (citationId.HasValue)
                {
                    model.Citations = await _chatSessionService.GetCitationsAsync(citationId.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workspace data.");
                model.ErrorMessage = $"Lỗi hệ thống: {ex.Message}";
            }

            return View(model);
        }
    }
}
