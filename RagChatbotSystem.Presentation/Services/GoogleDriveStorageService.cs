using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Services
{
    public class GoogleDriveStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<GoogleDriveStorageService> _logger;
        private readonly DriveService? _driveService;
        private readonly string? _targetFolderId;

        public GoogleDriveStorageService(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<GoogleDriveStorageService> _loggerInstance)
        {
            _environment = environment;
            _logger = _loggerInstance;

            var credPath = configuration["GoogleDrive:CredentialJsonPath"];
            _targetFolderId = configuration["GoogleDrive:TargetFolderId"];

            if (string.IsNullOrWhiteSpace(credPath))
            {
                _logger.LogWarning("Google Drive credential path is empty. Falling back to Local Storage.");
                return;
            }

            var absoluteCredPath = Path.IsPathRooted(credPath)
                ? credPath
                : Path.Combine(_environment.ContentRootPath, credPath);

            if (!File.Exists(absoluteCredPath))
            {
                _logger.LogWarning("Google Drive credential file was not found at '{Path}'. Falling back to Local Storage.", absoluteCredPath);
                return;
            }

            try
            {
#pragma warning disable CS0618
                var credential = GoogleCredential.FromFile(absoluteCredPath)
                    .CreateScoped(DriveService.Scope.DriveFile);
#pragma warning restore CS0618

                _driveService = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "RagChatbotSystem"
                });

                _logger.LogInformation("Google Drive storage initialized successfully. Uploaded files will be stored on Google Drive.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Google Drive storage. Falling back to Local Storage.");
            }
        }

        public async Task<StoredFileDto> SaveDatasetFileAsync(Guid datasetId, Stream fileStream, string fileName, long fileSize, CancellationToken cancellationToken = default)
        {
            if (_driveService != null)
            {
                try
                {
                    var fileMetadata = new Google.Apis.Drive.v3.Data.File
                    {
                        Name = fileName,
                        Parents = string.IsNullOrWhiteSpace(_targetFolderId) ? null : new List<string> { _targetFolderId }
                    };

                    FilesResource.CreateMediaUpload request;
                    request = _driveService.Files.Create(fileMetadata, fileStream, GetMimeType(fileName));

                    var progress = await request.UploadAsync(cancellationToken);
                    if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
                    {
                        throw new InvalidOperationException($"Google Drive upload failed: {progress.Exception?.Message}", progress.Exception);
                    }

                    var uploadedFile = request.ResponseBody;
                    var fileId = uploadedFile.Id;
                    var relativePath = $"gdrive://{fileId}?name={Uri.EscapeDataString(fileName)}";
                    var fileType = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();

                    _logger.LogInformation("Successfully uploaded file '{FileName}' to Google Drive with ID: {FileId}", fileName, fileId);

                    return new StoredFileDto(fileName, fileName, relativePath, fileType, fileSize);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Google Drive upload error. Falling back to local storage for file '{FileName}'", fileName);
                }
            }

            return await SaveDatasetFileLocallyAsync(datasetId, fileStream, fileName, fileSize, cancellationToken);
        }

        public async Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            if (relativePath.StartsWith("gdrive://", StringComparison.OrdinalIgnoreCase))
            {
                if (_driveService == null)
                {
                    throw new InvalidOperationException("Google Drive service is not initialized, cannot download file.");
                }

                var fileId = ExtractFileId(relativePath);
                var stream = new MemoryStream();
                await _driveService.Files.Get(fileId).DownloadAsync(stream, cancellationToken);
                stream.Position = 0;
                return stream;
            }

            return await OpenReadLocallyAsync(relativePath, cancellationToken);
        }

        public async Task DeleteFileIfExistsAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            if (relativePath.StartsWith("gdrive://", StringComparison.OrdinalIgnoreCase))
            {
                if (_driveService == null)
                {
                    _logger.LogWarning("Google Drive service is not initialized, cannot delete file {Path}.", relativePath);
                    return;
                }

                var fileId = ExtractFileId(relativePath);
                try
                {
                    await _driveService.Files.Delete(fileId).ExecuteAsync(cancellationToken);
                    _logger.LogInformation("Deleted file {FileId} from Google Drive.", fileId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file {FileId} from Google Drive.", fileId);
                }
                return;
            }

            await DeleteFileLocallyIfExistsAsync(relativePath, cancellationToken);
        }

        #region Local Storage Helper Methods

        private async Task<StoredFileDto> SaveDatasetFileLocallyAsync(Guid datasetId, Stream fileStream, string fileName, long fileSize, CancellationToken cancellationToken)
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

        private Task<Stream> OpenReadLocallyAsync(string relativePath, CancellationToken cancellationToken)
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

        private Task DeleteFileLocallyIfExistsAsync(string relativePath, CancellationToken cancellationToken)
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

        #endregion

        #region Helper Methods

        private static string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }

        private static string ExtractFileId(string relativePath)
        {
            var prefix = "gdrive://";
            if (!relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid Google Drive path format.");
            }
            var withoutPrefix = relativePath.Substring(prefix.Length);
            var queryIndex = withoutPrefix.IndexOf('?');
            return queryIndex >= 0 ? withoutPrefix.Substring(0, queryIndex) : withoutPrefix;
        }

        #endregion
    }
}
