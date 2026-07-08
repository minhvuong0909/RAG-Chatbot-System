using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IDocumentService
    {
        Task<IReadOnlyList<DocumentDto>> GetDocumentsByDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default);
        Task<DocumentDto?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
        Task<DocumentDto> UploadDocumentAsync(Guid datasetId, Guid userId, Stream fileStream, string fileName, long fileSize, CancellationToken cancellationToken = default);
        Task<DocumentDto> ProcessUploadedDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
        Task<Document?> ProcessAndIndexDocumentAsync(Guid datasetId, Guid userId, string fileName, string rawText);
        Task<bool> DeleteDocumentAsync(Guid documentId, Guid deletedBy, CancellationToken cancellationToken = default);
        Task<bool> HasCompletedDocumentsAsync(Guid datasetId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ChunkDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default);
        Task<DocumentPreviewDto?> GetDocumentPreviewAsync(Guid documentId, Guid currentUserId, string role, CancellationToken cancellationToken = default);
    }
}
