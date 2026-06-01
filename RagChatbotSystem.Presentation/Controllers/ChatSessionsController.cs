using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Controllers
{
    [Authorize]
    public class ChatSessionsController : Controller
    {
        private readonly IChatSessionService _chatSessionService;
        private readonly IChatService _chatService;
        private readonly ILogger<ChatSessionsController> _logger;

        public ChatSessionsController(
            IChatSessionService chatSessionService,
            IChatService chatService,
            ILogger<ChatSessionsController> logger)
        {
            _chatSessionService = chatSessionService;
            _chatService = chatService;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid datasetId, string? title)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var currentUserId))
            {
                return Challenge();
            }

            try
            {
                var session = await _chatSessionService.CreateSessionAsync(new CreateChatSessionRequest(currentUserId, datasetId, title));
                return RedirectToAction("Index", "Home", new { datasetId, sessionId = session.SessionId, success = "Khởi tạo phòng chat mới thành công!" });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Index", "Home", new { datasetId, error = $"Không thể khởi tạo phòng chat: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(Guid datasetId, Guid sessionId, string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return RedirectToAction("Index", "Home", new { datasetId, sessionId, error = "Câu hỏi không được để trống." });
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var currentUserId))
            {
                return Challenge();
            }

            try
            {
                // Bảo mật: Đảm bảo Session này thuộc về người dùng đang đăng nhập
                var session = await _chatSessionService.GetSessionAsync(sessionId);
                if (session == null || session.UserId != currentUserId)
                {
                    return RedirectToAction("Index", "Home", new { datasetId, error = "Bạn không có quyền gửi tin nhắn trong phòng chat này." });
                }


                await _chatService.SendChatMessageAsync(sessionId, question);
                return RedirectToAction("Index", "Home", new { datasetId, sessionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send chat message.");
                return RedirectToAction("Index", "Home", new { datasetId, sessionId, error = $"Lỗi gửi tin nhắn: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessageAjax(Guid datasetId, Guid sessionId, string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return BadRequest(new { error = "Câu hỏi không được để trống." });
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var currentUserId))
            {
                return Unauthorized();
            }

            try
            {
                var session = await _chatSessionService.GetSessionAsync(sessionId);
                if (session == null || session.UserId != currentUserId)
                {
                    return Forbid();
                }

                var response = await _chatService.SendChatMessageAsync(sessionId, question, HttpContext.RequestAborted);
                return Ok(new {
                    userMessage = new {
                        messageId = response.UserMessage.MessageId,
                        content = response.UserMessage.Content,
                        role = response.UserMessage.Role,
                        createdAt = response.UserMessage.CreatedAt.ToString("HH:mm")
                    },
                    assistantMessage = new {
                        messageId = response.AssistantMessage.MessageId,
                        content = response.AssistantMessage.Content,
                        role = response.AssistantMessage.Role,
                        createdAt = response.AssistantMessage.CreatedAt.ToString("HH:mm")
                    },
                    citations = response.Citations.Select(c => new {
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
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCitations(Guid messageId)
        {
            try
            {
                var citations = await _chatSessionService.GetCitationsAsync(messageId);
                return Ok(citations.Select(c => new {
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
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
