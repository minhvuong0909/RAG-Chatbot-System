using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;

        public LocalFileStorageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<StoredFileDto> SaveDatasetFileAsync(Guid datasetId, Stream fileStream, string fileName, long fileSize, CancellationToken cancellationToken = default)
        {
            var originalFileName = Path.GetFileName(fileName);
            var extension = Path.GetExtension(originalFileName);
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var relativeDirectory = Path.Combine("uploads", "datasets", datasetId.ToString());
            var absoluteDirectory = Path.Combine(_environment.WebRootPath, relativeDirectory);

            Directory.CreateDirectory(absoluteDirectory);

            var absolutePath = Path.Combine(absoluteDirectory, storedFileName);
            await using (var output = File.Create(absolutePath))
            {
                await fileStream.CopyToAsync(output, cancellationToken);
            }

            var relativePath = Path.Combine(relativeDirectory, storedFileName).Replace('\\', '/');
            var fileType = extension.TrimStart('.').ToLowerInvariant();

            return new StoredFileDto(
                originalFileName,
                storedFileName,
                relativePath,
                fileType,
                fileSize);
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new FileNotFoundException("Stored file path is empty.");
            }

            var absolutePath = ResolveInsideWebRoot(relativePath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("Stored file was not found.", relativePath);
            }

            Stream stream = File.OpenRead(absolutePath);
            return Task.FromResult(stream);
        }

        public Task DeleteFileIfExistsAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return Task.CompletedTask;
            }

            var absolutePath = ResolveInsideWebRoot(relativePath);
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }

            return Task.CompletedTask;
        }

        private string ResolveInsideWebRoot(string relativePath)
        {
            var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var absolutePath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, normalizedPath));
            var webRoot = Path.GetFullPath(_environment.WebRootPath);

            if (!absolutePath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Stored file path is outside the web root.");
            }

            return absolutePath;
        }
    }
}
