using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Controllers
{
    [ApiController]
    [Route("api/chat-sessions")]
    public class ChatSessionsController : ControllerBase
    {
        private readonly IChatSessionService _chatSessionService;
        private readonly IChatService _chatService;

        public ChatSessionsController(IChatSessionService chatSessionService, IChatService chatService)
        {
            _chatSessionService = chatSessionService;
            _chatService = chatService;
        }

        [HttpGet]
        public async Task<IActionResult> GetSessions([FromQuery] Guid? userId, [FromQuery] Guid? datasetId, CancellationToken cancellationToken)
        {
            var sessions = await _chatSessionService.GetSessionsAsync(userId, datasetId, cancellationToken);
            return Ok(sessions);
        }

        [HttpGet("{sessionId:guid}")]
        public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken cancellationToken)
        {
            var session = await _chatSessionService.GetSessionAsync(sessionId, cancellationToken);
            return session == null ? NotFound() : Ok(session);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSession([FromBody] CreateChatSessionRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var session = await _chatSessionService.CreateSessionAsync(request, cancellationToken);
                return CreatedAtAction(nameof(GetSession), new { sessionId = session.SessionId }, session);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet("{sessionId:guid}/messages")]
        public async Task<IActionResult> GetMessageHistory(Guid sessionId, CancellationToken cancellationToken)
        {
            try
            {
                var messages = await _chatSessionService.GetMessageHistoryAsync(sessionId, cancellationToken);
                return Ok(messages);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("{sessionId:guid}/messages")]
        public async Task<IActionResult> SendMessage(Guid sessionId, [FromBody] SendChatMessageRequest request)
        {
            var question = request.Question ?? request.Content;
            if (string.IsNullOrWhiteSpace(question))
            {
                return BadRequest(new { message = "Message content is required." });
            }

            try
            {
                var response = await _chatService.SendChatMessageAsync(sessionId, question.Trim());
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("messages/{messageId:guid}/citations")]
        public async Task<IActionResult> GetCitations(Guid messageId, CancellationToken cancellationToken)
        {
            try
            {
                var citations = await _chatSessionService.GetCitationsAsync(messageId, cancellationToken);
                return Ok(citations);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
