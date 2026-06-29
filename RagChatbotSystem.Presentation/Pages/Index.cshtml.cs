using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Helpers;
using RagChatbotSystem.Presentation.Realtime;

namespace RagChatbotSystem.Presentation.Pages
{
    [Authorize]
    [RequestSizeLimit(52428800)]
    public class IndexModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly IDatasetService _datasetService;
        private readonly IDocumentService _documentService;
        private readonly IChatSessionService _chatSessionService;
        private readonly IChatService _chatService;
        private readonly IQuestionSuggestionService _questionSuggestionService;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IUserService userService,
            IDatasetService datasetService,
            IDocumentService documentService,
            IChatSessionService chatSessionService,
            IChatService chatService,
            IQuestionSuggestionService questionSuggestionService,
            IRealtimeNotifier realtimeNotifier,
            ILogger<IndexModel> logger)
        {
            _userService = userService;
            _datasetService = datasetService;
            _documentService = documentService;
            _chatSessionService = chatSessionService;
            _chatService = chatService;
            _questionSuggestionService = questionSuggestionService;
            _realtimeNotifier = realtimeNotifier;
            _logger = logger;
        }

        public Guid? SelectedUserId { get; set; }
        public Guid? SelectedDatasetId { get; set; }
        public Guid? SelectedSessionId { get; set; }
        public Guid? SelectedMessageId { get; set; }
        public UserDto? SelectedUser { get; set; }
        public DatasetDto? SelectedDataset { get; set; }
        public ChatSessionDto? SelectedSession { get; set; }
        public IReadOnlyList<UserDto> Users { get; set; } = Array.Empty<UserDto>();
        public IReadOnlyList<DatasetDto> Datasets { get; set; } = Array.Empty<DatasetDto>();
        public IReadOnlyList<DocumentDto> Documents { get; set; } = Array.Empty<DocumentDto>();
        public IReadOnlyList<ChatSessionDto> Sessions { get; set; } = Array.Empty<ChatSessionDto>();
        public IReadOnlyList<ChatMessageDto> MessageHistory { get; set; } = Array.Empty<ChatMessageDto>();
        public IReadOnlyList<CitationDto> Citations { get; set; } = Array.Empty<CitationDto>();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(
            Guid? datasetId = null,
            Guid? sessionId = null,
            Guid? citationId = null,
            string? error = null,
            string? success = null)
        {
            if (!TryGetCurrentUser(out var currentUserId, out var currentUserRole))
            {
                return RedirectToPage("/Account/Login");
            }

            if (currentUserRole == "Admin")
            {
                return RedirectToPage("/Admin/Index");
            }

            if (string.Equals(User.FindFirstValue("MustChangePassword"), "True", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Account/ChangePassword");
            }

            SelectedUserId = currentUserId;
            SelectedDatasetId = datasetId;
            SelectedSessionId = sessionId;
            SelectedMessageId = citationId;
            ErrorMessage = error;
            SuccessMessage = success;

            try
            {
                SelectedUser = await _userService.GetUserAsync(currentUserId, HttpContext.RequestAborted);
                if (SelectedUser == null)
                {
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    TempData["ErrorMessage"] = "Your login session is no longer valid. Please sign in again.";
                    return RedirectToPage("/Account/Login");
                }

                Datasets = await _datasetService.GetDatasetsForUserAsync(currentUserId, currentUserRole, HttpContext.RequestAborted);

                if (datasetId.HasValue)
                {
                    SelectedDataset = await _datasetService.GetDatasetAsync(datasetId.Value, HttpContext.RequestAborted);
                    if (SelectedDataset != null)
                    {
                        var hasAccess = currentUserRole == "Admin" || Datasets.Any(d => d.DatasetId == datasetId.Value);
                        if (!hasAccess)
                        {
                            return RedirectToPage("/Index", new { error = "You do not have permission to access this dataset." });
                        }

                        Documents = await _documentService.GetDocumentsByDatasetAsync(datasetId.Value, HttpContext.RequestAborted);
                        Sessions = await _chatSessionService.GetSessionsAsync(currentUserId, datasetId.Value, HttpContext.RequestAborted);
                    }
                }

                if (sessionId.HasValue)
                {
                    SelectedSession = await _chatSessionService.GetSessionAsync(sessionId.Value, HttpContext.RequestAborted);
                    if (SelectedSession != null)
                    {
                        if (currentUserRole != "Admin" && SelectedSession.UserId != currentUserId)
                        {
                            return RedirectToPage("/Index", new { datasetId, error = "Access denied to this chat session." });
                        }

                        MessageHistory = await _chatSessionService.GetMessageHistoryAsync(sessionId.Value, HttpContext.RequestAborted);
                    }
                }

                if (citationId.HasValue)
                {
                    Citations = await _chatSessionService.GetCitationsAsync(citationId.Value, HttpContext.RequestAborted);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workspace data.");
                ErrorMessage = $"Loi he thong: {ex.Message}";
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
                return RedirectToPage("/Index", new { error = "Subject name is required." });
            }

            try
            {
                var dataset = await _datasetService.CreateDatasetAsync(
                    new CreateDatasetRequest(name, description, currentUserId, isPublic),
                    HttpContext.RequestAborted);

                await _realtimeNotifier.DatasetChangedAsync("created", dataset, HttpContext.RequestAborted);

                return RedirectToPage("/Index", new { datasetId = dataset.DatasetId, success = $"Subject '{dataset.Name}' created successfully." });
            }
            catch (Exception ex)
            {
                return RedirectToPage("/Index", new { error = ex.Message });
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
                return RedirectToPage("/Index", new { error = "Subject was not found." });
            }

            if (role != "Admin" && dataset.CreatedBy != currentUserId)
            {
                return Forbid();
            }

            try
            {
                var deleted = await _datasetService.DeleteDatasetAsync(id, HttpContext.RequestAborted);
                if (deleted)
                {
                    await _realtimeNotifier.DatasetChangedAsync("deleted", dataset, HttpContext.RequestAborted);
                }

                return RedirectToPage("/Index", new { success = deleted ? "Subject deleted successfully." : "Subject was not found." });
            }
            catch (Exception ex)
            {
                return RedirectToPage("/Index", new { error = $"Delete subject failed: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostUploadAsync(Guid datasetId, IFormFile file)
        {
            if (!User.IsInRole("Teacher") && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return RedirectToPage("/Index", new { datasetId, error = "Please select a file to upload." });
            }

            if (file.Length > 52428800)
            {
                return RedirectToPage("/Index", new { datasetId, error = "File size exceeds the allowed limit (max 50MB)." });
            }

            if (!TryGetCurrentUser(out var currentUserId, out var userRole))
            {
                return Challenge();
            }

            var dataset = await _datasetService.GetDatasetAsync(datasetId, HttpContext.RequestAborted);
            if (dataset == null)
            {
                return NotFound();
            }

            if (!await _datasetService.CanManageDatasetAsync(currentUserId, userRole, datasetId, HttpContext.RequestAborted))
            {
                return RedirectToPage("/Index", new { datasetId, error = "You only have permission to upload documents to assigned subjects." });
            }

            DocumentDto? uploadedDocument = null;
            try
            {
                await using var stream = file.OpenReadStream();
                var doc = await _documentService.UploadDocumentAsync(datasetId, currentUserId, stream, file.FileName, file.Length, HttpContext.RequestAborted);
                uploadedDocument = doc;
                await _realtimeNotifier.DocumentProgressAsync(datasetId, doc, "uploaded", 15, HttpContext.RequestAborted);

                await _realtimeNotifier.DocumentProgressAsync(datasetId, doc with { Status = "Processing" }, "processing", 45, HttpContext.RequestAborted);
                var processedDoc = await _documentService.ProcessUploadedDocumentAsync(doc.DocumentId, HttpContext.RequestAborted);
                await _realtimeNotifier.DocumentProgressAsync(datasetId, processedDoc, "completed", 100, HttpContext.RequestAborted);

                return RedirectToPage("/Index", new { datasetId, success = $"Document '{file.FileName}' uploaded and processed successfully." });
            }
            catch (Exception ex)
            {
                if (uploadedDocument != null)
                {
                    await _realtimeNotifier.DocumentProgressAsync(datasetId, uploadedDocument with { Status = "Failed" }, "failed", 100, HttpContext.RequestAborted);
                }

                _logger.LogError(ex, "Failed to upload and process document.");
                return RedirectToPage("/Index", new { datasetId, error = $"Document upload failed: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostDeleteDocumentAsync(Guid datasetId, Guid documentId)
        {
            if (!User.IsInRole("Teacher") && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (!TryGetCurrentUser(out var currentUserId, out var userRole))
            {
                return Challenge();
            }

            var document = await _documentService.GetDocumentAsync(documentId, HttpContext.RequestAborted);
            if (document == null)
            {
                return RedirectToPage("/Index", new { datasetId, error = "Khong tim thay tai lieu can xoa." });
            }

            if (!await _datasetService.CanManageDatasetAsync(currentUserId, userRole, document.DatasetId, HttpContext.RequestAborted))
            {
                return RedirectToPage("/Index", new { datasetId, error = "Ban chi co quyen xoa tai lieu trong mon hoc duoc Admin phan cong." });
            }

            try
            {
                var deleted = await _documentService.DeleteDocumentAsync(documentId);
                if (deleted)
                {
                    await _realtimeNotifier.DocumentProgressAsync(datasetId, document with { Status = "Deleted" }, "deleted", 100, HttpContext.RequestAborted);
                }

                return RedirectToPage("/Index", new
                {
                    datasetId,
                    success = deleted ? "Xoa tai lieu thanh cong." : null,
                    error = deleted ? null : "Xoa tai lieu that bai."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete document.");
                return RedirectToPage("/Index", new { datasetId, error = $"Xoa tai lieu that bai: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostCreateSessionAsync(Guid datasetId, string? title)
        {
            if (!TryGetCurrentUser(out var currentUserId, out var role))
            {
                return Challenge();
            }

            try
            {
                if (!await IsCurrentUserStillValidAsync(currentUserId))
                {
                    return await SignOutStaleUserAsync();
                }

                var allowedDatasets = await _datasetService.GetDatasetsForUserAsync(currentUserId, role, HttpContext.RequestAborted);
                if (!allowedDatasets.Any(d => d.DatasetId == datasetId))
                {
                    return RedirectToPage("/Index", new { error = "You do not have access to this subject." });
                }

                var session = await _chatSessionService.CreateSessionAsync(
                    new CreateChatSessionRequest(currentUserId, datasetId, title),
                    HttpContext.RequestAborted);

                await _realtimeNotifier.ChatSessionChangedAsync(currentUserId, datasetId, session, "created", HttpContext.RequestAborted);

                return RedirectToPage("/Index", new { datasetId, sessionId = session.SessionId, success = "Khoi tao phong chat moi thanh cong!" });
            }
            catch (Exception ex)
            {
                return RedirectToPage("/Index", new { datasetId, error = $"Khong the khoi tao phong chat: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostSendMessageAsync(Guid datasetId, Guid sessionId, string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return RedirectToPage("/Index", new { datasetId, sessionId, error = "Cau hoi khong duoc de trong." });
            }

            if (!TryGetCurrentUser(out var currentUserId, out _))
            {
                return Challenge();
            }

            try
            {
                if (!await IsCurrentUserStillValidAsync(currentUserId))
                {
                    return await SignOutStaleUserAsync();
                }

                var session = await _chatSessionService.GetSessionAsync(sessionId, HttpContext.RequestAborted);
                if (session == null || session.UserId != currentUserId)
                {
                    return RedirectToPage("/Index", new { datasetId, error = "Ban khong co quyen gui tin nhan trong phong chat nay." });
                }

                var response = await _chatService.SendChatMessageAsync(sessionId, question, HttpContext.RequestAborted);
                await _realtimeNotifier.ChatMessageSavedAsync(currentUserId, datasetId, sessionId, response, HttpContext.RequestAborted);
                return RedirectToPage("/Index", new { datasetId, sessionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send chat message.");
                return RedirectToPage("/Index", new { datasetId, sessionId, error = $"Loi gui tin nhan: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostSendMessageAjaxAsync(Guid datasetId, Guid sessionId, string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return new BadRequestObjectResult(new { error = "Cau hoi khong duoc de trong." });
            }

            if (!TryGetCurrentUser(out var currentUserId, out _))
            {
                return new UnauthorizedResult();
            }

            try
            {
                if (!await IsCurrentUserStillValidAsync(currentUserId))
                {
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return new UnauthorizedObjectResult(new { error = "Your login session is no longer valid. Please sign in again." });
                }

                var session = await _chatSessionService.GetSessionAsync(sessionId, HttpContext.RequestAborted);
                if (session == null || session.UserId != currentUserId)
                {
                    return new ForbidResult();
                }

                var response = await _chatService.SendChatMessageAsync(sessionId, question, HttpContext.RequestAborted);
                await _realtimeNotifier.ChatMessageSavedAsync(currentUserId, datasetId, sessionId, response, HttpContext.RequestAborted);

                return new JsonResult(new
                {
                    userMessage = new
                    {
                        messageId = response.UserMessage.MessageId,
                        content = response.UserMessage.Content,
                        role = response.UserMessage.Role,
                        createdAt = VietnamTime.Format(response.UserMessage.CreatedAt, "HH:mm")
                    },
                    assistantMessage = new
                    {
                        messageId = response.AssistantMessage.MessageId,
                        content = response.AssistantMessage.Content,
                        role = response.AssistantMessage.Role,
                        createdAt = VietnamTime.Format(response.AssistantMessage.CreatedAt, "HH:mm")
                    },
                    citations = response.Citations.Select(c => new
                    {
                        citationId = c.CitationId,
                        fileName = c.FileName,
                        pageNumber = c.PageNumber,
                        quoteText = c.QuoteText,
                        sourceLabel = c.SourceLabel
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send AJAX chat message.");
                return new ObjectResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status500InternalServerError };
            }
        }

        public async Task<IActionResult> OnGetGetCitationsAsync(Guid messageId)
        {
            try
            {
                var citations = await _chatSessionService.GetCitationsAsync(messageId, HttpContext.RequestAborted);
                return new JsonResult(citations.Select(c => new
                {
                    citationId = c.CitationId,
                    fileName = c.FileName,
                    pageNumber = c.PageNumber,
                    quoteText = c.QuoteText,
                    sourceLabel = c.SourceLabel
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get citations for message {MessageId}", messageId);
                return new ObjectResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status500InternalServerError };
            }
        }

        public async Task<IActionResult> OnGetGetChunksAsync(Guid documentId)
        {
            try
            {
                var chunks = await _documentService.GetDocumentChunksAsync(documentId, HttpContext.RequestAborted);
                return new JsonResult(chunks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve document chunks.");
                return new BadRequestObjectResult(new { error = $"Khong the lay danh sach phan doan: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnGetSuggestQuestionsAsync(Guid datasetId)
        {
            if (!TryGetCurrentUser(out var currentUserId, out var role))
            {
                return new UnauthorizedResult();
            }

            try
            {
                if (!await IsCurrentUserStillValidAsync(currentUserId))
                {
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return new UnauthorizedObjectResult(new { error = "Your login session is no longer valid. Please sign in again." });
                }

                var allowedDatasets = await _datasetService.GetDatasetsForUserAsync(currentUserId, role, HttpContext.RequestAborted);
                if (!allowedDatasets.Any(d => d.DatasetId == datasetId))
                {
                    return new ForbidResult();
                }

                var result = await _questionSuggestionService.SuggestQuestionsAsync(datasetId, HttpContext.RequestAborted);
                return new JsonResult(new
                {
                    questions = result.Questions,
                    warning = result.Warning
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate suggested questions for dataset {DatasetId}.", datasetId);
                return new ObjectResult(new { error = $"Khong the tao cau hoi goi y: {ex.Message}" }) { StatusCode = StatusCodes.Status500InternalServerError };
            }
        }

        private bool TryGetCurrentUser(out Guid userId, out string role)
        {
            role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdString, out userId);
        }

        private async Task<bool> IsCurrentUserStillValidAsync(Guid currentUserId)
        {
            return await _userService.GetUserAsync(currentUserId, HttpContext.RequestAborted) != null;
        }

        private async Task<IActionResult> SignOutStaleUserAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["ErrorMessage"] = "Your login session is no longer valid. Please sign in again.";
            return RedirectToPage("/Account/Login");
        }
    }
}
