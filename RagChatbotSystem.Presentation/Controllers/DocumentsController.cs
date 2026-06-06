using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Controllers
{
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly IDocumentService _documentService;
        private readonly IDatasetService _datasetService;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(
            IDocumentService documentService,
            IDatasetService datasetService,
            ILogger<DocumentsController> logger)
        {
            _documentService = documentService;
            _datasetService = datasetService;
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Roles = "Teacher,Admin")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> Upload(Guid datasetId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return RedirectToAction("Index", "Home", new { datasetId, error = "Vui long chon tep de tai len." });
            }

            if (file.Length > 52428800)
            {
                return RedirectToAction("Index", "Home", new { datasetId, error = "Kich thuoc tep vuot qua gioi han cho phep (toi da 50MB)." });
            }

            if (!TryGetCurrentUser(out var currentUserId, out var userRole))
            {
                return Challenge();
            }

            var dataset = await _datasetService.GetDatasetAsync(datasetId);
            if (dataset == null)
            {
                return NotFound();
            }

            if (!await _datasetService.CanManageDatasetAsync(currentUserId, userRole, datasetId))
            {
                return RedirectToAction("Index", "Home", new { datasetId, error = "Ban chi co quyen tai len tai lieu vao mon hoc duoc Admin phan cong." });
            }

            try
            {
                using var stream = file.OpenReadStream();
                var doc = await _documentService.UploadDocumentAsync(datasetId, currentUserId, stream, file.FileName, file.Length);
                await _documentService.ProcessUploadedDocumentAsync(doc.DocumentId);

                return RedirectToAction("Index", "Home", new { datasetId, success = $"Tai lieu '{file.FileName}' da duoc tai len va xu ly thanh cong." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload and process document.");
                return RedirectToAction("Index", "Home", new { datasetId, error = $"Tai tai lieu that bai: {ex.Message}" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Teacher,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid datasetId, Guid documentId)
        {
            if (!TryGetCurrentUser(out var currentUserId, out var userRole))
            {
                return Challenge();
            }

            var document = await _documentService.GetDocumentAsync(documentId);
            if (document == null)
            {
                return RedirectToAction("Index", "Home", new { datasetId, error = "Khong tim thay tai lieu can xoa." });
            }

            if (!await _datasetService.CanManageDatasetAsync(currentUserId, userRole, document.DatasetId))
            {
                return RedirectToAction("Index", "Home", new { datasetId, error = "Ban chi co quyen xoa tai lieu trong mon hoc duoc Admin phan cong." });
            }

            try
            {
                var deleted = await _documentService.DeleteDocumentAsync(documentId);
                return deleted
                    ? RedirectToAction("Index", "Home", new { datasetId, success = "Xoa tai lieu thanh cong." })
                    : RedirectToAction("Index", "Home", new { datasetId, error = "Xoa tai lieu that bai." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete document.");
                return RedirectToAction("Index", "Home", new { datasetId, error = $"Xoa tai lieu that bai: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetChunks(Guid documentId)
        {
            try
            {
                var chunks = await _documentService.GetDocumentChunksAsync(documentId);
                return Json(chunks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve document chunks.");
                return BadRequest(new { error = $"Khong the lay danh sach phan doan: {ex.Message}" });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> Preview(Guid documentId)
        {
            if (!TryGetCurrentUser(out var currentUserId, out var userRole))
            {
                return Challenge();
            }

            try
            {
                var preview = await _documentService.GetDocumentPreviewAsync(documentId, currentUserId, userRole);
                if (preview == null)
                {
                    return NotFound();
                }

                return View(preview);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        private bool TryGetCurrentUser(out Guid userId, out string role)
        {
            role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdString, out userId);
        }
    }
}
