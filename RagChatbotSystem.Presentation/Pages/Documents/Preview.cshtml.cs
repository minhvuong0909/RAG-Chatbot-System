using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Pages.Documents
{
    [Authorize(Roles = "Teacher,Admin")]
    public class PreviewModel : PageModel
    {
        private readonly IDocumentService _documentService;

        public PreviewModel(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        public DocumentPreviewDto Preview { get; private set; } = null!;

        public Guid DocumentId => Preview.DocumentId;
        public Guid DatasetId => Preview.DatasetId;
        public string DatasetName => Preview.DatasetName;
        public string FileName => Preview.FileName;
        public string FileType => Preview.FileType;
        public long FileSize => Preview.FileSize;
        public string Status => Preview.Status;
        public DateTime UploadedAt => Preview.UploadedAt;
        public IReadOnlyList<DocumentChunkPreviewDto> Chunks => Preview.Chunks;

        public async Task<IActionResult> OnGetAsync(Guid documentId)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            if (!Guid.TryParse(userIdValue, out var currentUserId))
            {
                return Challenge();
            }

            try
            {
                var preview = await _documentService.GetDocumentPreviewAsync(documentId, currentUserId, role, HttpContext.RequestAborted);
                if (preview == null)
                {
                    return NotFound();
                }

                Preview = preview;
                return Page();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }
    }
}
