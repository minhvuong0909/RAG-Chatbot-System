using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestRagController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IDocumentService _documentService;
        private readonly IChatService _chatService;

        public TestRagController(AppDbContext context, IDocumentService documentService, IChatService chatService)
        {
            _context = context;
            _documentService = documentService;
            _chatService = chatService;
        }

        [HttpGet("run")]
        public async Task<IActionResult> RunTestFlow(
            [FromQuery] Guid documentId, 
            [FromQuery] string question = "Burj Khalifa nằm ở đâu?")
        {
            try
            {
                // 1. Tìm tài liệu theo documentId
                var document = await _context.Documents
                    .Include(d => d.Dataset)
                    .FirstOrDefaultAsync(d => d.DocumentId == documentId);

                if (document == null)
                {
                    return BadRequest(new { 
                        Status = "Error", 
                        Message = $"Không tìm thấy Document với ID: {documentId}. Vui lòng upload tài liệu trước." 
                    });
                }

                if (document.Status != "Completed")
                {
                    return BadRequest(new { 
                        Status = "Error", 
                        Message = $"Tài liệu '{document.FileName}' chưa được xử lý thành công (Trạng thái hiện tại: {document.Status}). Vui lòng xử lý tài liệu trước." 
                    });
                }

                var datasetId = document.DatasetId;
                var userId = document.UploadedBy;

                // 2. Tìm hoặc tạo mới Chat Session cho dataset này
                var session = await _context.ChatSessions
                    .FirstOrDefaultAsync(s => s.DatasetId == datasetId && s.UserId == userId);

                if (session == null)
                {
                    session = new ChatSession
                    {
                        SessionId = Guid.NewGuid(),
                        UserId = userId,
                        DatasetId = datasetId,
                        Title = $"Chat Test - {document.FileName}",
                        StartedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ChatSessions.Add(session);
                    await _context.SaveChangesAsync();
                }

                // 3. Gửi câu hỏi chat
                var chatResponse = await _chatService.SendChatMessageAsync(session.SessionId, question);

                // 4. Lấy các trích dẫn (citations) nguồn từ câu trả lời
                var citations = await _context.Citations
                    .Include(c => c.Chunk)
                    .Where(c => c.MessageId == chatResponse.AssistantMessage.MessageId)
                    .Select(c => new {
                        c.CitationId,
                        c.PageNumber,
                        c.QuoteText,
                        c.SourceLabel,
                        ChunkContent = c.Chunk.Content
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Status = "Success",
                    DocumentName = document.FileName,
                    Question = question,
                    Answer = chatResponse.AssistantMessage.Content,
                    SessionId = session.SessionId,
                    Citations = citations
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = "Error", Message = ex.Message, Inner = ex.InnerException?.Message });
            }
        }
    }
}
