using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;
using Pgvector;

namespace RagChatbotSystem.Business.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly AppDbContext _context;
        private readonly IRagApiClient _ragApiClient;

        public DocumentService(AppDbContext context, IRagApiClient ragApiClient)
        {
            _context = context;
            _ragApiClient = ragApiClient;
        }

        public async Task<Document?> ProcessAndIndexDocumentAsync(Guid datasetId, Guid userId, string fileName, string rawText)
        {
            // 1. Kiểm tra Dataset có tồn tại không
            var dataset = await _context.Datasets.FindAsync(datasetId);
            if (dataset == null)
            {
                throw new ArgumentException("Dataset not found.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 2. Tạo thực thể Document
                var document = new Document
                {
                    DocumentId = Guid.NewGuid(),
                    DatasetId = datasetId,
                    FileName = fileName,
                    FilePath = fileName,
                    FileSize = Encoding.UTF8.GetByteCount(rawText),
                    FileType = Path.GetExtension(fileName).TrimStart('.').ToLower(),
                    Status = "Processing",
                    UploadedBy = userId,
                    UploadedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Documents.Add(document);

                // 3. Tách nhỏ văn bản (Chunking)
                var textChunks = SplitText(rawText, 600, 120);
                if (textChunks.Count == 0)
                {
                    document.Status = "Completed";
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return document;
                }

                // Khởi tạo danh sách với capacity xác định (tránh resize động)
                var chunkEntities = new List<Chunk>(textChunks.Count);
                var apiDocs = new List<DocumentModelDto>(textChunks.Count);
                var documentIdStr = document.DocumentId.ToString();
                var datasetIdStr = datasetId.ToString();
                var now = DateTime.UtcNow;

                for (int i = 0; i < textChunks.Count; i++)
                {
                    var chunkId = Guid.NewGuid();
                    var content = textChunks[i];

                    chunkEntities.Add(new Chunk
                    {
                        ChunkId = chunkId,
                        DatasetId = datasetId,
                        DocumentId = document.DocumentId,
                        ChunkIndex = i + 1,
                        Content = content,
                        PageNumber = 1,
                        CreatedAt = now,
                        MetadataJson = $"{{\"index\": {i + 1}}}"
                    });

                    // Payload gửi sang Python API
                    apiDocs.Add(new DocumentModelDto
                    {
                        PageContent = content,
                        Metadata = new Dictionary<string, object>(3)
                        {
                            { "id", chunkId.ToString() },
                            { "document_id", documentIdStr },
                            { "dataset_id", datasetIdStr }
                        }
                    });
                }

                // Thêm tất cả chunks vào DbContext (chưa ghi DB)
                _context.Chunks.AddRange(chunkEntities);

                // 4. Gửi chunks sang Python RAG API để đánh index
                var indexRequest = new IndexRequestDto
                {
                    Documents = apiDocs,
                    RebuildCache = true
                };

                var indexResponse = await _ragApiClient.IndexDocumentsAsync(indexRequest);

                if (indexResponse?.Embeddings == null || indexResponse.Embeddings.Count != chunkEntities.Count)
                {
                    throw new Exception($"Failed to index documents in RAG API or mismatched embedding count. Expected {chunkEntities.Count}, got {indexResponse?.Embeddings?.Count ?? 0}. Message: {indexResponse?.Message}");
                }

                // 5. Tạo bản ghi Vector từ embeddings trả về
                var vectorRecords = new List<VectorRecord>(chunkEntities.Count);
                for (int i = 0; i < chunkEntities.Count; i++)
                {
                    vectorRecords.Add(new VectorRecord
                    {
                        VectorId = Guid.NewGuid(),
                        DatasetId = datasetId,
                        DocumentId = document.DocumentId,
                        ChunkId = chunkEntities[i].ChunkId,
                        Embedding = new Vector(indexResponse.Embeddings[i]),
                        EmbeddingModel = "sentence-transformers/all-MiniLM-L6-v2",
                        CreatedAt = now
                    });
                }

                _context.VectorRecords.AddRange(vectorRecords);

                // Cập nhật trạng thái tài liệu
                document.Status = "Completed";

                // Ghi tất cả thay đổi vào DB trong 1 lần duy nhất (tối ưu I/O)
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return document;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error processing document: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Tách văn bản theo kỹ thuật cửa sổ trượt (Sliding Window).
        /// Preallocate capacity cho List để tránh resize động.
        /// </summary>
        private static List<string> SplitText(string text, int chunkSize, int chunkOverlap)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            int step = chunkSize - chunkOverlap;
            if (step <= 0) step = chunkSize;

            // Tính trước số lượng chunk để preallocate
            int estimatedCount = (text.Length / step) + 1;
            var chunks = new List<string>(estimatedCount);

            for (int i = 0; i < text.Length; i += step)
            {
                int length = Math.Min(chunkSize, text.Length - i);
                chunks.Add(text.Substring(i, length));
                if (i + length >= text.Length) break;
            }
            return chunks;
        }

        public async Task<bool> DeleteDocumentAsync(Guid documentId)
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null) return false;

            // 1. Gọi Python API để xóa các vector & chunk khỏi bộ nhớ cache FAISS/BM25
            var pythonDeleted = await _ragApiClient.DeleteDocumentAsync(documentId);
            if (!pythonDeleted)
            {
                Console.WriteLine($"Warning: Failed to delete document {documentId} from Python RAG index.");
            }

            // 2. Xóa khỏi cơ sở dữ liệu Postgres (EF Core tự động xóa cascade)
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
