using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Controllers
{
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;

        public DocumentsController(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        [HttpGet("api/datasets/{datasetId:guid}/documents")]
        public async Task<IActionResult> GetDocumentsByDataset(Guid datasetId, CancellationToken cancellationToken)
        {
            try
            {
                var documents = await _documentService.GetDocumentsByDatasetAsync(datasetId, cancellationToken);
                return Ok(documents);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet("api/documents/{documentId:guid}")]
        public async Task<IActionResult> GetDocument(Guid documentId, CancellationToken cancellationToken)
        {
            var document = await _documentService.GetDocumentAsync(documentId, cancellationToken);
            return document == null ? NotFound() : Ok(document);
        }

        [HttpPost("api/datasets/{datasetId:guid}/documents")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> UploadDocument(
            Guid datasetId,
            [FromForm] Guid uploadedBy,
            [FromForm] IFormFile file,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "File is required." });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var document = await _documentService.UploadDocumentAsync(
                    datasetId,
                    uploadedBy,
                    stream,
                    file.FileName,
                    file.Length,
                    cancellationToken);

                return CreatedAtAction(nameof(GetDocument), new { documentId = document.DocumentId }, document);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("api/documents/{documentId:guid}/process")]
        public async Task<IActionResult> ProcessDocument(Guid documentId, CancellationToken cancellationToken)
        {
            try
            {
                var document = await _documentService.ProcessUploadedDocumentAsync(documentId, cancellationToken);
                return Ok(document);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("api/documents/{documentId:guid}")]
        public async Task<IActionResult> DeleteDocument(Guid documentId)
        {
            var deleted = await _documentService.DeleteDocumentAsync(documentId);
            return deleted ? NoContent() : NotFound();
        }
    }
}
