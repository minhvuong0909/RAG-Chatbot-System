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
using RagChatbotSystem.Business.Exceptions;
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
        private readonly ITokenUsageService _tokenUsageService;
        private readonly ISystemSettingService _systemSettingService;
        private readonly ICreditService _creditService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IUserService userService,
            IDatasetService datasetService,
            IDocumentService documentService,
            IChatSessionService chatSessionService,
            IChatService chatService,
            IQuestionSuggestionService questionSuggestionService,
            IRealtimeNotifier realtimeNotifier,
            ITokenUsageService tokenUsageService,
            ISystemSettingService systemSettingService,
            ICreditService creditService,
            ILogger<IndexModel> logger)
        {
            _userService = userService;
            _datasetService = datasetService;
            _documentService = documentService;
            _chatSessionService = chatSessionService;
            _chatService = chatService;
            _questionSuggestionService = questionSuggestionService;
            _realtimeNotifier = realtimeNotifier;
            _tokenUsageService = tokenUsageService;
            _systemSettingService = systemSettingService;
            _creditService = creditService;
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
        public HashSet<Guid> MessageIdsWithCitations { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public int DailyTokensUsed { get; set; }
        public int DailyTokenLimit { get; set; }
        public CreditBalanceDto? CreditBalance { get; set; }

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

            if (currentUserRole == "Teacher" && !datasetId.HasValue)
            {
                return RedirectToPage("/Datasets/Index");
            }

            SelectedUserId = currentUserId;
            SelectedDatasetId = datasetId;
            SelectedSessionId = sessionId;
            SelectedMessageId = citationId;
            ErrorMessage = error;
            SuccessMessage = success;

            try
            {
                if (currentUserRole == "Student")
                {
                    DailyTokensUsed = await _tokenUsageService.GetDailyUsageAsync(currentUserId, HttpContext.RequestAborted);
                    DailyTokenLimit = await _systemSettingService.GetDailyTokenLimitAsync(HttpContext.RequestAborted);
                    CreditBalance = await _creditService.GetStudentCreditSummaryAsync(currentUserId, HttpContext.RequestAborted);
                }

                SelectedUser = await _userService.GetUserAsync(currentUserId, HttpContext.RequestAborted);
                if (SelectedUser == null)
                {
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    TempData["ErrorMessage"] = "Phiên đăng nhập không còn hiệu lực. Vui lòng đăng nhập lại.";
                    return RedirectToPage("/Account/Login");
                }

                Datasets = await _datasetService.GetDatasetsForUserAsync(currentUserId, currentUserRole, HttpContext.RequestAborted);

                if (datasetId.HasValue)
                {
                    SelectedDataset = await _datasetService.GetDatasetAsync(datasetId.Value, HttpContext.RequestAborted);
                    if (SelectedDataset != null)
                    {
                        var hasAccess = currentUserRole == "Admin" || Datasets.Any(d => d.DatasetId == datasetId.Value);
                        if (!hasAccess && SelectedDataset.IsArchived && sessionId.HasValue)
                        {
                            var historicalSession = await _chatSessionService.GetSessionAsync(sessionId.Value, HttpContext.RequestAborted);
                            hasAccess = historicalSession != null
                                && historicalSession.DatasetId == datasetId.Value
                                && (currentUserRole == "Admin" || historicalSession.UserId == currentUserId);
                        }
                        if (!hasAccess)
                        {
                            return RedirectToPage("/Index", new { error = "Bạn không có quyền truy cập môn học này." });
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
                            return RedirectToPage("/Index", new { datasetId, error = "Bạn không có quyền truy cập cuộc trò chuyện này." });
                        }

                        MessageHistory = await _chatSessionService.GetMessageHistoryAsync(sessionId.Value, HttpContext.RequestAborted);
                        foreach (var assistantMessage in MessageHistory.Where(m => string.Equals(m.Role, "Assistant", StringComparison.OrdinalIgnoreCase)))
                        {
                            var messageCitations = await _chatSessionService.GetCitationsAsync(assistantMessage.MessageId, HttpContext.RequestAborted);
                            if (messageCitations.Count > 0)
                            {
                                MessageIdsWithCitations.Add(assistantMessage.MessageId);
                            }
                        }
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
                ErrorMessage = "Không thể tải không gian học tập. Vui lòng thử lại sau.";
            }

            return Page();
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
                    return RedirectToPage("/Index", new { error = "Bạn không có quyền truy cập môn học này." });
                }

                if (!await _documentService.HasCompletedDocumentsAsync(datasetId, HttpContext.RequestAborted))
                {
                    return RedirectToPage("/Index", new { datasetId, error = "Môn học này chưa có tài liệu đã xử lý. Vui lòng tải tài liệu lên trước khi bắt đầu trò chuyện." });
                }

                var session = await _chatSessionService.CreateSessionAsync(
                    new CreateChatSessionRequest(currentUserId, datasetId, string.IsNullOrWhiteSpace(title) ? "Trò chuyện mới" : title.Trim()),
                    HttpContext.RequestAborted);

                await _realtimeNotifier.ChatSessionChangedAsync(currentUserId, datasetId, session, "created", HttpContext.RequestAborted);

                return RedirectToPage("/Index", new { datasetId, sessionId = session.SessionId, success = "Đã tạo cuộc trò chuyện mới." });
            }
            catch (Exception)
            {
                return RedirectToPage("/Index", new { datasetId, error = "Không thể tạo cuộc trò chuyện mới. Vui lòng thử lại." });
            }
        }

        public async Task<IActionResult> OnPostSendMessageAsync(Guid datasetId, Guid sessionId, string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return RedirectToPage("/Index", new { datasetId, sessionId, error = "Câu hỏi không được để trống." });
            }

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

                var session = await _chatSessionService.GetSessionAsync(sessionId, HttpContext.RequestAborted);
                if (session == null || session.UserId != currentUserId)
                {
                    return RedirectToPage("/Index", new { datasetId, error = "Bạn không có quyền gửi tin nhắn trong cuộc trò chuyện này." });
                }

                if (session.DatasetId != datasetId)
                {
                    return RedirectToPage("/Index", new { datasetId, error = "Cuộc trò chuyện này không thuộc môn học đã chọn." });
                }

                if (!await _datasetService.CanAccessActiveDatasetAsync(currentUserId, role, datasetId, HttpContext.RequestAborted))
                {
                    return RedirectToPage("/Index", new { error = "Môn học đã được lưu trữ hoặc quyền truy cập của bạn đã bị thu hồi." });
                }

                if (!await _documentService.HasCompletedDocumentsAsync(datasetId, HttpContext.RequestAborted))
                {
                    return RedirectToPage("/Index", new { datasetId, sessionId, error = "Môn học này chưa có tài liệu đã xử lý. Vui lòng tải tài liệu lên trước khi bắt đầu trò chuyện." });
                }

                var response = await _chatService.SendChatMessageAsync(sessionId, question, HttpContext.RequestAborted);
                await _realtimeNotifier.ChatMessageSavedAsync(currentUserId, datasetId, sessionId, response, HttpContext.RequestAborted);
                return RedirectToPage("/Index", new { datasetId, sessionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send chat message.");
                return RedirectToPage("/Index", new { datasetId, sessionId, error = "Không thể gửi câu hỏi. Vui lòng thử lại sau." });
            }
        }

        public async Task<IActionResult> OnPostSendMessageAjaxAsync(Guid datasetId, Guid sessionId, string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return new BadRequestObjectResult(new { error = "Câu hỏi không được để trống." });
            }

            if (!TryGetCurrentUser(out var currentUserId, out var currentUserRole))
            {
                return new UnauthorizedResult();
            }

            try
            {
                if (!await IsCurrentUserStillValidAsync(currentUserId))
                {
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return new UnauthorizedObjectResult(new { error = "Phiên đăng nhập không còn hiệu lực. Vui lòng đăng nhập lại." });
                }

                var session = await _chatSessionService.GetSessionAsync(sessionId, HttpContext.RequestAborted);
                if (session == null || session.UserId != currentUserId)
                {
                    return new ForbidResult();
                }

                if (session.DatasetId != datasetId)
                {
                    return new BadRequestObjectResult(new { error = "Cuộc trò chuyện này không thuộc môn học đã chọn." });
                }

                if (!await _datasetService.CanAccessActiveDatasetAsync(currentUserId, currentUserRole, datasetId, HttpContext.RequestAborted))
                {
                    return new ForbidResult();
                }

                if (!await _documentService.HasCompletedDocumentsAsync(datasetId, HttpContext.RequestAborted))
                {
                    return new BadRequestObjectResult(new { error = "Môn học này chưa có tài liệu đã xử lý. Vui lòng tải tài liệu lên trước khi bắt đầu trò chuyện." });
                }

                var response = await _chatService.SendChatMessageAsync(sessionId, question, HttpContext.RequestAborted);
                await _realtimeNotifier.ChatMessageSavedAsync(currentUserId, datasetId, sessionId, response, HttpContext.RequestAborted);

                int? dailyTokensUsed = null;
                int? dailyTokenLimit = null;
                CreditBalanceDto? creditBalance = response.CreditBalance;
                if (currentUserRole == "Student")
                {
                    dailyTokensUsed = await _tokenUsageService.GetDailyUsageAsync(currentUserId, HttpContext.RequestAborted);
                    dailyTokenLimit = await _systemSettingService.GetDailyTokenLimitAsync(HttpContext.RequestAborted);
                    creditBalance ??= await _creditService.GetStudentCreditSummaryAsync(currentUserId, HttpContext.RequestAborted);
                }

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
                        sourceLabel = c.SourceLabel,
                        documentId = c.DocumentId,
                        chunkId = c.ChunkId,
                        fileType = c.FileType,
                        chunkIndex = c.ChunkIndex
                    }),
                    dailyTokensUsed,
                    dailyTokenLimit,
                    creditBalance = creditBalance == null ? null : new
                    {
                        freeCredits = creditBalance.FreeCredits,
                        paidCredits = creditBalance.PaidCredits,
                        totalCredits = creditBalance.TotalCredits
                    },
                    creditSpend = response.CreditSpend == null ? null : new
                    {
                        calculatedCredits = response.CreditSpend.CalculatedCredits,
                        chargedCredits = response.CreditSpend.ChargedCredits,
                        freeCreditsUsed = response.CreditSpend.FreeCreditsUsed,
                        paidCreditsUsed = response.CreditSpend.PaidCreditsUsed,
                        wasInsufficientBalance = response.CreditSpend.WasInsufficientBalance,
                        wasActualTokenUsage = response.CreditSpend.WasActualTokenUsage,
                        modelName = response.CreditSpend.ModelName
                    }
                });
            }
            catch (ChatRequestBlockedException ex)
            {
                _logger.LogInformation(ex, "Blocked AJAX chat request for session {SessionId}: {Reason}.", sessionId, ex.Reason);
                var statusCode = ex.Reason == ChatBlockReason.InsufficientCredits
                    ? StatusCodes.Status402PaymentRequired
                    : StatusCodes.Status429TooManyRequests;
                return new ObjectResult(new
                {
                    error = ex.Message,
                    code = ex.Reason.ToString()
                }) { StatusCode = statusCode };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send AJAX chat message.");
                return new ObjectResult(new { error = "Không thể gửi câu hỏi. Vui lòng thử lại sau." }) { StatusCode = StatusCodes.Status500InternalServerError };
            }
        }

        public async Task<IActionResult> OnGetGetCitationsAsync(Guid messageId)
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
                    return new UnauthorizedObjectResult(new { error = "Phiên đăng nhập không còn hiệu lực. Vui lòng đăng nhập lại." });
                }

                var session = await _chatSessionService.GetSessionForMessageAsync(messageId, HttpContext.RequestAborted);
                if (session == null)
                {
                    return NotFound();
                }

                var allowedDatasets = await _datasetService.GetDatasetsForUserAsync(currentUserId, role, HttpContext.RequestAborted);
                var canAccessDataset = allowedDatasets.Any(d => d.DatasetId == session.DatasetId);
                var hasAccess = role == "Admin"
                    || (role == "Teacher" && canAccessDataset)
                    || session.UserId == currentUserId;
                if (!hasAccess)
                {
                    return new ForbidResult();
                }

                var citations = await _chatSessionService.GetCitationsAsync(messageId, HttpContext.RequestAborted);
                return new JsonResult(citations.Select(c => new
                {
                    citationId = c.CitationId,
                    fileName = c.FileName,
                    pageNumber = c.PageNumber,
                    quoteText = c.QuoteText,
                    sourceLabel = c.SourceLabel,
                    documentId = c.DocumentId,
                    chunkId = c.ChunkId,
                    fileType = c.FileType,
                    chunkIndex = c.ChunkIndex
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get citations for message {MessageId}", messageId);
                return new ObjectResult(new { error = "Không thể tải nguồn trích dẫn. Vui lòng thử lại sau." }) { StatusCode = StatusCodes.Status500InternalServerError };
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
                    return new UnauthorizedObjectResult(new { error = "Phiên đăng nhập không còn hiệu lực. Vui lòng đăng nhập lại." });
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
                return new ObjectResult(new { error = "Không thể tạo gợi ý câu hỏi. Vui lòng thử lại sau." }) { StatusCode = StatusCodes.Status500InternalServerError };
            }
        }

        public async Task<IActionResult> OnGetDocumentChunksAsync(Guid documentId)
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
                    return new UnauthorizedObjectResult(new { error = "Phiên đăng nhập không còn hiệu lực. Vui lòng đăng nhập lại." });
                }

                var doc = await _documentService.GetDocumentAsync(documentId, HttpContext.RequestAborted);
                if (doc == null)
                {
                    return NotFound();
                }

                var allowedDatasets = await _datasetService.GetDatasetsForUserAsync(currentUserId, role, HttpContext.RequestAborted);
                var ownsHistoricalEvidence = await _chatSessionService.CanUserAccessDocumentEvidenceAsync(currentUserId, documentId, HttpContext.RequestAborted);
                if (!allowedDatasets.Any(d => d.DatasetId == doc.DatasetId) && role != "Admin" && !ownsHistoricalEvidence)
                {
                    return new ForbidResult();
                }

                var chunks = await _documentService.GetDocumentChunksAsync(documentId, HttpContext.RequestAborted);
                return new JsonResult(new
                {
                    documentId = doc.DocumentId,
                    fileName = doc.FileName,
                    chunks = chunks.Select(c => new
                    {
                        chunkId = c.ChunkId,
                        chunkIndex = c.ChunkIndex,
                        pageNumber = c.PageNumber,
                        content = c.Content
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get document chunks for {DocumentId}", documentId);
                return new ObjectResult(new { error = "Không thể tải nội dung tài liệu. Vui lòng thử lại sau." }) { StatusCode = StatusCodes.Status500InternalServerError };
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
            TempData["ErrorMessage"] = "Phiên đăng nhập không còn hiệu lực. Vui lòng đăng nhập lại.";
            return RedirectToPage("/Account/Login");
        }
    }
}
