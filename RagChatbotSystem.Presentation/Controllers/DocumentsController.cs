using System;
using System.IO;
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
        [RequestSizeLimit(52428800)] // Giới hạn tải lên tối đa 50MB (50 * 1024 * 1024 bytes)
        public async Task<IActionResult> Upload(Guid datasetId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return RedirectToAction("Index", "Home", new { datasetId, error = "Vui lòng chọn tệp để tải lên." });
            }

            if (file.Length > 52428800)
            {
                return RedirectToAction("Index", "Home", new { datasetId, error = "Kích thước tệp vượt quá giới hạn cho phép (tối đa 50MB)." });
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var currentUserId))
            {
                return Challenge();
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);

            // Kiểm tra xem Dataset có tồn tại và thuộc quyền quản lý của User không
            var dataset = await _datasetService.GetDatasetAsync(datasetId);
            if (dataset == null)
            {
                return NotFound();
            }

            if (userRole != "Admin" && dataset.CreatedBy != currentUserId)
            {
                return RedirectToAction("Index", "Home", new { datasetId, error = "Bạn chỉ có quyền tải lên tài liệu vào Dataset của chính mình." });
            }

            try
            {
                using var stream = file.OpenReadStream();
                // 1. Lưu file vật lý
                var doc = await _documentService.UploadDocumentAsync(datasetId, currentUserId, stream, file.FileName, file.Length);
                // 2. Tiến hành trích xuất text + chunking + embedding + đánh chỉ mục
                await _documentService.ProcessUploadedDocumentAsync(doc.DocumentId);

                return RedirectToAction("Index", "Home", new { datasetId, success = $"Tài liệu '{file.FileName}' đã được tải lên và xử lý phân đoạn thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload and process document.");
                return RedirectToAction("Index", "Home", new { datasetId, error = $"Tải tài liệu thất bại: {ex.Message}" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Teacher,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid datasetId, Guid documentId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var currentUserId))
            {
                return Challenge();
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);

            // Kiểm tra sự tồn tại của Document
            var document = await _documentService.GetDocumentAsync(documentId);
            if (document == null)
            {
                return RedirectToAction("Index", "Home", new { datasetId, error = "Không tìm thấy tài liệu cần xóa." });
            }

            // Giáo viên chỉ được xóa tài liệu do chính mình tải lên
            if (userRole != "Admin" && document.UploadedBy != currentUserId)
            {
                return RedirectToAction("Index", "Home", new { datasetId, error = "Bạn chỉ có quyền xóa tài liệu do chính mình tải lên." });
            }

            try
            {
                var deleted = await _documentService.DeleteDocumentAsync(documentId);
                if (deleted)
                {
                    return RedirectToAction("Index", "Home", new { datasetId, success = "Xóa tài liệu thành công!" });
                }
                return RedirectToAction("Index", "Home", new { datasetId, error = "Xóa tài liệu thất bại." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete document.");
                return RedirectToAction("Index", "Home", new { datasetId, error = $"Xóa tài liệu thất bại: {ex.Message}" });
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
                return BadRequest(new { error = $"Không thể lấy danh sách phân đoạn: {ex.Message}" });
            }
        }
    }
}
