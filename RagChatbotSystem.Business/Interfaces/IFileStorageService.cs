using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IFileStorageService
    {
        Task<StoredFileDto> SaveDatasetFileAsync(Guid datasetId, Stream fileStream, string fileName, long fileSize, CancellationToken cancellationToken = default);
        Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
        Task DeleteFileIfExistsAsync(string relativePath, CancellationToken cancellationToken = default);
    }
}
