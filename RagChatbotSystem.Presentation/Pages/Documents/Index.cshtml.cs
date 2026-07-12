using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Realtime;

namespace RagChatbotSystem.Presentation.Pages.Documents
{
    [Authorize(Roles = "Teacher,Admin")]
    [RequestSizeLimit(52428800)] // Limit to 50MB
    public class IndexModel : PageModel
    {
        private static readonly HashSet<string> AllowedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".docx",
            ".txt"
        };

        private readonly IDatasetService _datasetService;
        private readonly IDocumentService _documentService;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IDatasetService datasetService,
            IDocumentService documentService,
            IRealtimeNotifier realtimeNotifier,
            ILogger<IndexModel> logger)
        {
            _datasetService = datasetService;
            _documentService = documentService;
            _realtimeNotifier = realtimeNotifier;
            _logger = logger;
        }

        public DatasetDto Dataset { get; private set; } = null!;
        public IReadOnlyList<DocumentDto> Documents { get; private set; } = Array.Empty<DocumentDto>();
        public Guid DatasetId { get; private set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid datasetId, string? error = null, string? success = null)
        {
            if (!TryGetCurrentUser(out var currentUserId, out var role))
            {
                return Challenge();
            }

            DatasetId = datasetId;
            ErrorMessage = error;
            SuccessMessage = success;

            try
            {
                var dataset = await _datasetService.GetDatasetAsync(datasetId, HttpContext.RequestAborted);
                if (dataset == null)
                {
                    return NotFound();
                }
                Dataset = dataset;

                if (!await _datasetService.CanManageDatasetAsync(currentUserId, role, datasetId, HttpContext.RequestAborted))
                {
                    return Forbid();
                }

                Documents = await _documentService.GetDocumentsByDatasetAsync(datasetId, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading document workspace.");
                ErrorMessage = "Không thể tải danh sách tài liệu. Vui lòng thử lại sau.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync(Guid datasetId, IFormFile file)
        {
            if (!User.IsInRole("Teacher") && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return RedirectToPage("/Documents/Index", new { datasetId, error = "Vui lòng chọn tệp để tải lên." });
            }

            if (file.Length > 52428800)
            {
                return RedirectToPage("/Documents/Index", new { datasetId, error = "Dung lượng tệp vượt quá giới hạn 50 MB." });
            }

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedFileExtensions.Contains(extension))
            {
                return RedirectToPage("/Documents/Index", new { datasetId, error = "Chỉ hỗ trợ tệp PDF, DOCX và TXT." });
            }

            if (!TryGetCurrentUser(out var currentUserId, out var userRole))
            {
                return Challenge();
            }

            try
            {
                var dataset = await _datasetService.GetDatasetAsync(datasetId, HttpContext.RequestAborted);
                if (dataset == null)
                {
                    return NotFound();
                }

                if (!await _datasetService.CanManageDatasetAsync(currentUserId, userRole, datasetId, HttpContext.RequestAborted))
                {
                    return RedirectToPage("/Documents/Index", new { datasetId, error = "Bạn chỉ có thể tải tài liệu lên các môn học được phân công." });
                }

                DocumentDto? uploadedDocument = null;
                try
                {
                    await using var stream = file.OpenReadStream();
                    var doc = await _documentService.UploadDocumentAsync(
                        datasetId,
                        currentUserId,
                        stream,
                        file.FileName,
                        file.Length,
                        false,
                        HttpContext.RequestAborted);
                    uploadedDocument = doc;
                    await _realtimeNotifier.DocumentProgressAsync(datasetId, doc, "uploaded", 15, HttpContext.RequestAborted);

                    await _realtimeNotifier.DocumentProgressAsync(datasetId, doc with { Status = "Processing" }, "processing", 45, HttpContext.RequestAborted);
                    var processedDoc = await _documentService.ProcessUploadedDocumentAsync(doc.DocumentId, HttpContext.RequestAborted);
                    await _realtimeNotifier.DocumentProgressAsync(datasetId, processedDoc, "completed", 100, HttpContext.RequestAborted);

                    return RedirectToPage("/Documents/Index", new { datasetId, success = $"Đã tải lên và xử lý tài liệu \"{file.FileName}\"." });
                }
                catch (Exception ex)
                {
                    if (uploadedDocument != null)
                    {
                        await _realtimeNotifier.DocumentProgressAsync(datasetId, uploadedDocument with { Status = "Failed" }, "failed", 100, HttpContext.RequestAborted);
                    }

                    _logger.LogError(ex, "Failed to upload and process document.");
                    var errorMessage = ex.Message.Contains("file name already exists", StringComparison.OrdinalIgnoreCase)
                        ? "Tài liệu có cùng tên tệp đã tồn tại. Vui lòng xác nhận thay thế tài liệu hiện tại trước khi tải lên."
                        : "Không thể tải lên và xử lý tài liệu. Vui lòng thử lại hoặc chọn tệp khác.";
                    return RedirectToPage("/Documents/Index", new { datasetId, error = errorMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed authorization check before upload.");
                return RedirectToPage("/Documents/Index", new { datasetId, error = "Không thể kiểm tra quyền tải tài liệu. Vui lòng thử lại sau." });
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

            try
            {
                var document = await _documentService.GetDocumentAsync(documentId, HttpContext.RequestAborted);
                if (document == null)
                {
                    return RedirectToPage("/Documents/Index", new { datasetId, error = "Không tìm thấy tài liệu." });
                }

                if (!await _datasetService.CanManageDatasetAsync(currentUserId, userRole, document.DatasetId, HttpContext.RequestAborted))
                {
                    return RedirectToPage("/Documents/Index", new { datasetId, error = "Bạn chỉ có thể xóa tài liệu trong các môn học được phân công." });
                }

                var deleted = await _documentService.DeleteDocumentAsync(documentId, currentUserId, HttpContext.RequestAborted);
                if (deleted)
                {
                    await _realtimeNotifier.DocumentProgressAsync(datasetId, document with { Status = "Deleted" }, "deleted", 100, HttpContext.RequestAborted);
                }

                return RedirectToPage("/Documents/Index", new
                {
                    datasetId,
                    success = deleted ? $"Đã xóa tài liệu \"{document.FileName}\" khỏi danh sách sử dụng của chatbot." : null,
                    error = deleted ? null : "Không thể xóa tài liệu. Vui lòng thử lại."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete document.");
                return RedirectToPage("/Documents/Index", new { datasetId, error = "Không thể xóa tài liệu. Vui lòng thử lại sau." });
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
