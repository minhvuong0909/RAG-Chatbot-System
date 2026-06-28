using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using WordText = DocumentFormat.OpenXml.Wordprocessing.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Repositories;
using RagChatbotSystem.DataAccess.Models;
using UglyToad.PdfPig;

namespace RagChatbotSystem.Business.Services
{
    public class DocumentService : IDocumentService
    {
        private const string EmbeddingModel = "sentence-transformers/all-MiniLM-L6-v2";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<Document> _documentRepository;
        private readonly IGenericRepository<Dataset> _datasetRepository;
        private readonly IGenericRepository<User> _userRepository;
        private readonly IGenericRepository<Chunk> _chunkRepository;
        private readonly IGenericRepository<VectorRecord> _vectorRecordRepository;
        private readonly IRagApiClient _ragApiClient;
        private readonly IFileStorageService _fileStorageService;
        private readonly IRealtimeService _realtimeService;
        private readonly ISystemSettingService _systemSettingService;
        private readonly ILogger<DocumentService> _logger;

        public DocumentService(
            IUnitOfWork unitOfWork,
            IRagApiClient ragApiClient,
            IFileStorageService fileStorageService,
            IRealtimeService realtimeService,
            ISystemSettingService systemSettingService,
            ILogger<DocumentService> logger)
        {
            _unitOfWork = unitOfWork;
            _documentRepository = _unitOfWork.Repository<Document>();
            _datasetRepository = _unitOfWork.Repository<Dataset>();
            _userRepository = _unitOfWork.Repository<User>();
            _chunkRepository = _unitOfWork.Repository<Chunk>();
            _vectorRecordRepository = _unitOfWork.Repository<VectorRecord>();
            _ragApiClient = ragApiClient;
            _fileStorageService = fileStorageService;
            _realtimeService = realtimeService;
            _systemSettingService = systemSettingService;
            _logger = logger;
        }

        public async Task<IReadOnlyList<DocumentDto>> GetDocumentsByDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default)
        {
            var datasetExists = await _datasetRepository.GetQueryable().AnyAsync(d => d.DatasetId == datasetId, cancellationToken);
            if (!datasetExists)
            {
                throw new KeyNotFoundException("Dataset was not found.");
            }

            return await _documentRepository.GetQueryable()
                .AsNoTracking()
                .Where(d => d.DatasetId == datasetId)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new DocumentDto(
                    d.DocumentId,
                    d.DatasetId,
                    d.FileName,
                    d.FilePath,
                    d.FileType,
                    d.FileSize,
                    d.Status,
                    d.UploadedBy,
                    d.UploadedAt,
                    d.UpdatedAt))
                .ToListAsync(cancellationToken);
        }

        public async Task<DocumentDto?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            return await _documentRepository.GetQueryable()
                .AsNoTracking()
                .Where(d => d.DocumentId == documentId)
                .Select(d => new DocumentDto(
                    d.DocumentId,
                    d.DatasetId,
                    d.FileName,
                    d.FilePath,
                    d.FileType,
                    d.FileSize,
                    d.Status,
                    d.UploadedBy,
                    d.UploadedAt,
                    d.UpdatedAt))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<DocumentDto> UploadDocumentAsync(Guid datasetId, Guid userId, Stream fileStream, string fileName, long fileSize, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name is required.", nameof(fileName));
            }

            if (fileSize <= 0)
            {
                throw new ArgumentException("File must not be empty.", nameof(fileSize));
            }

            var datasetExists = await _datasetRepository.GetQueryable().AnyAsync(d => d.DatasetId == datasetId, cancellationToken);
            if (!datasetExists)
            {
                throw new InvalidOperationException("Dataset was not found.");
            }

            var userExists = await _userRepository.GetQueryable().AnyAsync(u => u.UserId == userId, cancellationToken);
            if (!userExists)
            {
                throw new InvalidOperationException("Uploader user was not found.");
            }

            var storedFile = await _fileStorageService.SaveDatasetFileAsync(datasetId, fileStream, fileName, fileSize, cancellationToken);
            var now = DateTime.UtcNow;

            var document = new Document
            {
                DocumentId = Guid.NewGuid(),
                DatasetId = datasetId,
                FileName = storedFile.OriginalFileName,
                FilePath = storedFile.RelativePath,
                FileType = storedFile.FileType,
                FileSize = storedFile.FileSize,
                Status = "Uploaded",
                UploadedBy = userId,
                UploadedAt = now,
                UpdatedAt = now
            };

            await _documentRepository.AddAsync(document, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ToDto(document);
        }

        public async Task<DocumentDto> ProcessUploadedDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            var document = await _documentRepository.GetQueryable().FirstOrDefaultAsync(d => d.DocumentId == documentId, cancellationToken);
            if (document == null)
            {
                throw new KeyNotFoundException("Document was not found.");
            }

            document.Status = "Processing";
            document.UpdatedAt = DateTime.UtcNow;
            _documentRepository.Update(document);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _realtimeService.SendDocumentProgressAsync(document.DatasetId, document.DocumentId, "Đang xử lý", 10, cancellationToken);

            try
            {
                await _realtimeService.SendDocumentProgressAsync(document.DatasetId, document.DocumentId, "Đang trích xuất chữ", 30, cancellationToken);
                await using var stream = await _fileStorageService.OpenReadAsync(document.FilePath, cancellationToken);
                var segments = await ExtractTextSegmentsAsync(stream, document.FileType, cancellationToken);

                await _realtimeService.SendDocumentProgressAsync(document.DatasetId, document.DocumentId, "Đang phân tích đoạn", 50, cancellationToken);
                var chunkSize = await _systemSettingService.GetChunkSizeAsync(cancellationToken);
                var chunkOverlap = await _systemSettingService.GetChunkOverlapAsync(cancellationToken);
                var chunks = SplitTextSegments(segments, chunkSize, chunkOverlap);

                if (chunks.Count == 0)
                {
                    throw new InvalidOperationException("No extractable text found.");
                }

                await _realtimeService.SendDocumentProgressAsync(document.DatasetId, document.DocumentId, "Đang nhúng vector & index", 75, cancellationToken);
                await IndexExistingDocumentAsync(document, chunks, cancellationToken);

                document.Status = "Completed";
                document.UpdatedAt = DateTime.UtcNow;
                _documentRepository.Update(document);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _realtimeService.SendDocumentProgressAsync(document.DatasetId, document.DocumentId, "Hoàn thành", 100, cancellationToken);
                await _realtimeService.TriggerUiUpdateAsync("Document", document.DocumentId, cancellationToken);

                return ToDto(document);
            }
            catch (Exception ex)
            {
                _unitOfWork.ClearTracker();

                var failedDocument = await _documentRepository.GetQueryable().FirstOrDefaultAsync(d => d.DocumentId == documentId, cancellationToken);
                if (failedDocument != null)
                {
                    failedDocument.Status = "Failed";
                    failedDocument.UpdatedAt = DateTime.UtcNow;
                    _documentRepository.Update(failedDocument);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _realtimeService.SendDocumentProgressAsync(failedDocument.DatasetId, failedDocument.DocumentId, $"Thất bại: {ex.Message}", 0, cancellationToken);
                }

                throw;
            }
        }

        public async Task<Document?> ProcessAndIndexDocumentAsync(Guid datasetId, Guid userId, string fileName, string rawText)
        {
            var dataset = await _datasetRepository.GetByIdAsync(datasetId);
            if (dataset == null)
            {
                throw new ArgumentException("Dataset not found.");
            }

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;
                var document = new Document
                {
                    DocumentId = Guid.NewGuid(),
                    DatasetId = datasetId,
                    FileName = fileName,
                    FilePath = fileName,
                    FileSize = Encoding.UTF8.GetByteCount(rawText),
                    FileType = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
                    Status = "Processing",
                    UploadedBy = userId,
                    UploadedAt = now,
                    UpdatedAt = now
                };

                await _documentRepository.AddAsync(document);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                return document;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing document '{FileName}' for dataset {DatasetId}.", fileName, datasetId);
                throw;
            }
        }

        public async Task<bool> DeleteDocumentAsync(Guid documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null) return false;

            var pythonDeleted = await _ragApiClient.DeleteDocumentAsync(documentId);
            if (!pythonDeleted)
            {
                _logger.LogWarning("Failed to delete document {DocumentId} from Python RAG index.", documentId);
            }

            _documentRepository.Delete(document);
            await _unitOfWork.SaveChangesAsync();
            await _fileStorageService.DeleteFileIfExistsAsync(document.FilePath);

            await _realtimeService.TriggerUiUpdateAsync("Document", documentId);

            return true;
        }

        public async Task<DocumentPreviewDto?> GetDocumentPreviewAsync(Guid documentId, Guid currentUserId, string role, CancellationToken cancellationToken = default)
        {
            var document = await _documentRepository.GetQueryable()
                .AsNoTracking()
                .Include(d => d.Dataset)
                .Where(d => d.DocumentId == documentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (document == null)
            {
                return null;
            }

            if (role != "Admin")
            {
                if (role != "Teacher")
                {
                    throw new UnauthorizedAccessException("Only teachers and admins can preview documents.");
                }

                var assignmentRepo = _unitOfWork.Repository<TeacherSubjectAssignment>();
                var canManage = await assignmentRepo.GetQueryable()
                    .AnyAsync(a => a.DatasetId == document.DatasetId && a.TeacherId == currentUserId, cancellationToken);

                if (!canManage)
                {
                    throw new UnauthorizedAccessException("You do not manage this subject.");
                }
            }

            var chunks = await _chunkRepository.GetQueryable()
                .AsNoTracking()
                .Where(c => c.DocumentId == documentId)
                .OrderBy(c => c.ChunkIndex)
                .Select(c => new DocumentChunkPreviewDto(
                    c.ChunkId,
                    c.ChunkIndex,
                    c.PageNumber,
                    c.Content,
                    c.MetadataJson))
                .ToListAsync(cancellationToken);

            return new DocumentPreviewDto(
                document.DocumentId,
                document.DatasetId,
                document.Dataset.Name,
                document.FileName,
                document.FileType,
                document.FileSize,
                document.Status,
                document.UploadedAt,
                chunks);
        }

        internal static async Task<List<ExtractedTextSegment>> ExtractTextSegmentsAsync(Stream stream, string fileType, CancellationToken cancellationToken = default)
        {
            var normalizedType = fileType.TrimStart('.').ToLowerInvariant();

            return normalizedType switch
            {
                "txt" => await ExtractTxtAsync(stream, cancellationToken),
                "pdf" => ExtractPdf(stream),
                "docx" => ExtractDocx(stream),
                _ => throw new NotSupportedException($"File type '{fileType}' is not supported.")
            };
        }

        internal static List<TextChunk> SplitTextSegments(IReadOnlyList<ExtractedTextSegment> segments, int chunkSize, int chunkOverlap)
        {
            var nonEmptySegments = segments.Where(s => !string.IsNullOrWhiteSpace(s.Text)).ToList();
            if (nonEmptySegments.Count == 0) return new List<TextChunk>();

            var builder = new StringBuilder();
            var ranges = new List<PageTextRange>(nonEmptySegments.Count);

            foreach (var segment in nonEmptySegments)
            {
                if (builder.Length > 0)
                {
                    builder.Append("\n\n");
                }

                var start = builder.Length;
                builder.Append(segment.Text.Trim());
                ranges.Add(new PageTextRange(start, builder.Length, segment.PageNumber));
            }

            var text = builder.ToString();
            var step = chunkSize - chunkOverlap;
            if (step <= 0) step = chunkSize;

            var estimatedCount = (text.Length / step) + 1;
            var chunks = new List<TextChunk>(estimatedCount);

            for (var i = 0; i < text.Length; i += step)
            {
                var length = Math.Min(chunkSize, text.Length - i);
                var content = text.Substring(i, length).Trim();

                if (!string.IsNullOrWhiteSpace(content))
                {
                    chunks.Add(new TextChunk(content, ResolveDominantPage(i, i + length, ranges)));
                }

                if (i + length >= text.Length) break;
            }

            return chunks;
        }

        private async Task IndexExistingDocumentAsync(Document document, IReadOnlyList<TextChunk> chunks, CancellationToken cancellationToken)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var existingChunks = await _chunkRepository.GetQueryable()
                    .Where(c => c.DocumentId == document.DocumentId)
                    .ToListAsync(cancellationToken);

                if (existingChunks.Count > 0)
                {
                    await _ragApiClient.DeleteDocumentAsync(document.DocumentId);
                    _chunkRepository.DeleteRange(existingChunks);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                await AddIndexedChunksAsync(document, chunks, rebuildCache: false, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task AddIndexedChunksAsync(Document document, IReadOnlyList<TextChunk> textChunks, bool rebuildCache, CancellationToken cancellationToken)
        {
            var chunkEntities = new List<Chunk>(textChunks.Count);
            var apiDocs = new List<DocumentModelDto>(textChunks.Count);
            var now = DateTime.UtcNow;

            for (var i = 0; i < textChunks.Count; i++)
            {
                var chunkId = Guid.NewGuid();
                var chunkIndex = i + 1;
                var textChunk = textChunks[i];

                chunkEntities.Add(new Chunk
                {
                    ChunkId = chunkId,
                    DatasetId = document.DatasetId,
                    DocumentId = document.DocumentId,
                    ChunkIndex = chunkIndex,
                    Content = textChunk.Content,
                    PageNumber = textChunk.PageNumber,
                    CreatedAt = now,
                    MetadataJson = $"{{\"chunk_index\": {chunkIndex}, \"page_number\": {textChunk.PageNumber}}}"
                });

                apiDocs.Add(new DocumentModelDto
                {
                    PageContent = textChunk.Content,
                    Metadata = new Dictionary<string, object>(6)
                    {
                        { "id", chunkId.ToString() },
                        { "document_id", document.DocumentId.ToString() },
                        { "dataset_id", document.DatasetId.ToString() },
                        { "file_name", document.FileName },
                        { "page_number", textChunk.PageNumber },
                        { "chunk_index", chunkIndex }
                    }
                });
            }

            await _chunkRepository.AddRangeAsync(chunkEntities, cancellationToken);

            var indexResponse = await _ragApiClient.IndexDocumentsAsync(new IndexRequestDto
            {
                Documents = apiDocs,
                RebuildCache = rebuildCache
            });

            if (indexResponse.Embeddings == null || indexResponse.Embeddings.Count != chunkEntities.Count)
            {
                throw new InvalidOperationException($"Failed to index documents in RAG API or mismatched embedding count. Expected {chunkEntities.Count}, got {indexResponse.Embeddings?.Count ?? 0}. Message: {indexResponse.Message}");
            }

            var vectorRecords = new List<VectorRecord>(chunkEntities.Count);
            for (var i = 0; i < chunkEntities.Count; i++)
            {
                vectorRecords.Add(new VectorRecord
                {
                    VectorId = Guid.NewGuid(),
                    DatasetId = document.DatasetId,
                    DocumentId = document.DocumentId,
                    ChunkId = chunkEntities[i].ChunkId,
                    Embedding = new Vector(indexResponse.Embeddings[i]),
                    EmbeddingModel = EmbeddingModel,
                    CreatedAt = now
                });
            }

            await _vectorRecordRepository.AddRangeAsync(vectorRecords, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private static async Task<List<ExtractedTextSegment>> ExtractTxtAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(text)
                ? new List<ExtractedTextSegment>()
                : new List<ExtractedTextSegment> { new(text, 1) };
        }

        private static List<ExtractedTextSegment> ExtractPdf(Stream stream)
        {
            var segments = new List<ExtractedTextSegment>();
            using var pdf = PdfDocument.Open(stream);

            foreach (var page in pdf.GetPages())
            {
                var words = page.GetWords();
                var text = string.Join(" ", words.Select(w => w.Text));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    segments.Add(new ExtractedTextSegment(text, page.Number));
                }
            }

            return segments;
        }

        private static List<ExtractedTextSegment> ExtractDocx(Stream stream)
        {
            using var document = WordprocessingDocument.Open(stream, false);
            var body = document.MainDocumentPart?.Document.Body;
            if (body == null)
            {
                return new List<ExtractedTextSegment>();
            }

            var text = string.Join("\n", body.Descendants<WordText>().Select(t => t.Text));
            return string.IsNullOrWhiteSpace(text)
                ? new List<ExtractedTextSegment>()
                : new List<ExtractedTextSegment> { new(text, 1) };
        }

        private static int ResolveDominantPage(int chunkStart, int chunkEnd, IReadOnlyList<PageTextRange> ranges)
        {
            var pageScores = new Dictionary<int, int>();

            foreach (var range in ranges)
            {
                var overlapStart = Math.Max(chunkStart, range.Start);
                var overlapEnd = Math.Min(chunkEnd, range.End);
                var overlap = Math.Max(0, overlapEnd - overlapStart);

                if (overlap == 0) continue;

                pageScores[range.PageNumber] = pageScores.TryGetValue(range.PageNumber, out var current)
                    ? current + overlap
                    : overlap;
            }

            return pageScores.Count == 0
                ? 1
                : pageScores.OrderByDescending(p => p.Value).ThenBy(p => p.Key).First().Key;
        }

        public async Task<IReadOnlyList<ChunkDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            var chunks = await _chunkRepository.GetQueryable()
                .AsNoTracking()
                .Include(c => c.VectorRecord)
                .Where(c => c.DocumentId == documentId)
                .OrderBy(c => c.ChunkIndex)
                .ToListAsync(cancellationToken);

            return chunks.Select(c => new ChunkDto(
                c.ChunkId,
                c.DocumentId,
                c.ChunkIndex,
                c.Content,
                c.PageNumber,
                c.VectorRecord?.Embedding?.ToArray()
            )).ToList();
        }

        private static DocumentDto ToDto(Document document)
        {
            return new DocumentDto(
                document.DocumentId,
                document.DatasetId,
                document.FileName,
                document.FilePath,
                document.FileType,
                document.FileSize,
                document.Status,
                document.UploadedBy,
                document.UploadedAt,
                document.UpdatedAt);
        }
    }

    internal sealed record ExtractedTextSegment(string Text, int PageNumber);
    internal sealed record TextChunk(string Content, int PageNumber);
    internal sealed record PageTextRange(int Start, int End, int PageNumber);
}
