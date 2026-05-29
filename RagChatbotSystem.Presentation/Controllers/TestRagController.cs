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
        public async Task<IActionResult> RunTestFlow([FromQuery] string question = "Where is the Burj Khalifa?")
        {
            try
            {
                // 1. Ensure we have a test user
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == "testuser@example.com");
                if (user == null)
                {
                    user = new User
                    {
                        UserId = Guid.NewGuid(),
                        Email = "testuser@example.com",
                        FullName = "Test User",
                        Role = "User",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                // 2. Ensure we have a test dataset (always recreate to force clean re-indexing in tests)
                var dataset = await _context.Datasets.FirstOrDefaultAsync(d => d.Name == "Test Dataset");
                if (dataset != null)
                {
                    _context.Datasets.Remove(dataset);
                    await _context.SaveChangesAsync();
                }

                dataset = new Dataset
                {
                    DatasetId = Guid.NewGuid(),
                    Name = "Test Dataset",
                    Description = "Dataset created for integration testing",
                    CreatedBy = user.UserId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Datasets.Add(dataset);
                await _context.SaveChangesAsync();

                // 3. Check if we already indexed a document
                var document = await _context.Documents.FirstOrDefaultAsync(d => d.DatasetId == dataset.DatasetId);
                if (document == null)
                {
                    var textContent = 
                        "The Burj Khalifa is the tallest building in the world, located in Dubai, United Arab Emirates. It has a total height of 829.8 m (2,722 ft).\n\n" +
                        "The Eiffel Tower is a wrought-iron lattice tower on the Champ de Mars in Paris, France. It is named after the engineer Gustave Eiffel.\n\n" +
                        "The Great Wall of China is a series of fortifications that were built across the historical northern borders of ancient Chinese states.\n\n" +
                        "Python is a high-level, general-purpose programming language. Its design philosophy emphasizes code readability with the use of significant indentation.";

                    document = await _documentService.ProcessAndIndexDocumentAsync(
                        dataset.DatasetId, 
                        user.UserId, 
                        "test_facts.txt", 
                        textContent
                    );
                }

                // 4. Ensure we have a chat session
                var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.DatasetId == dataset.DatasetId);
                if (session == null)
                {
                    session = new ChatSession
                    {
                        SessionId = Guid.NewGuid(),
                        UserId = user.UserId,
                        DatasetId = dataset.DatasetId,
                        Title = "Test Chat Session",
                        StartedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ChatSessions.Add(session);
                    await _context.SaveChangesAsync();
                }

                // 5. Send chat query
                var chatResponse = await _chatService.SendChatMessageAsync(session.SessionId, question);

                // 6. Fetch citations
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
