using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Data;

namespace RagChatbotSystem.Presentation.Services
{
    public class RagIndexRehydrationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RagIndexRehydrationService> _logger;

        public RagIndexRehydrationService(
            IServiceScopeFactory scopeFactory,
            ILogger<RagIndexRehydrationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            const int maxAttempts = 18;
            for (var attempt = 1; attempt <= maxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
            {
                try
                {
                    await RehydrateAsync(stoppingToken);
                    return;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to rehydrate RAG index from database. Attempt {Attempt}/{MaxAttempts}.", attempt, maxAttempts);

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                    }
                }
            }
        }

        private async Task RehydrateAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ragApiClient = scope.ServiceProvider.GetRequiredService<IRagApiClient>();

            var documents = await db.Chunks
                .AsNoTracking()
                .Include(c => c.Document)
                .Where(c => c.Document.Status == "Completed")
                .OrderBy(c => c.DocumentId)
                .ThenBy(c => c.ChunkIndex)
                .Select(c => new DocumentModelDto
                {
                    PageContent = c.Content,
                    Metadata = new Dictionary<string, object>
                    {
                        { "id", c.ChunkId.ToString() },
                        { "document_id", c.DocumentId.ToString() },
                        { "dataset_id", c.DatasetId.ToString() },
                        { "file_name", c.Document.FileName },
                        { "file_type", c.Document.FileType },
                        { "page_number", c.PageNumber },
                        { "chunk_index", c.ChunkIndex }
                    }
                })
                .ToListAsync(cancellationToken);

            if (documents.Count == 0)
            {
                _logger.LogInformation("Skipped RAG index rehydration because no completed chunks were found.");
                return;
            }

            var response = await ragApiClient.IndexDocumentsAsync(new IndexRequestDto
            {
                Documents = documents,
                RebuildCache = true
            });

            _logger.LogInformation("Rehydrated RAG index with {ChunkCount} chunks. RAG response: {Message}", documents.Count, response.Message);
        }
    }
}
