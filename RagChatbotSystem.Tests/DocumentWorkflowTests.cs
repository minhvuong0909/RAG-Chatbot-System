using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Tests;

public class DocumentWorkflowTests
{
    [Fact]
    public async Task UploadDocumentAsync_BlocksDuplicateActiveFileInSameSubject()
    {
        await using var context = CreateContext();
        var datasetId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedDatasetAndUser(context, datasetId, userId);

        var service = CreateService(context);
        var firstFile = NewTextStream("same content");
        await service.UploadDocumentAsync(datasetId, userId, firstFile, "outline.txt", firstFile.Length);

        var duplicateFile = NewTextStream("same content");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadDocumentAsync(datasetId, userId, duplicateFile, "copy.txt", duplicateFile.Length));

        Assert.Contains("This document already exists in this subject.", ex.Message);
        Assert.Equal(1, await context.Documents.CountAsync());
    }

    [Fact]
    public async Task UploadDocumentAsync_BlocksSameFileNameUntilOverwriteConfirmed()
    {
        await using var context = CreateContext();
        var datasetId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedDatasetAndUser(context, datasetId, userId);

        var service = CreateService(context);
        var firstFile = NewTextStream("old content");
        await service.UploadDocumentAsync(datasetId, userId, firstFile, "outline.txt", firstFile.Length);

        var replacementFile = NewTextStream("new content");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadDocumentAsync(datasetId, userId, replacementFile, "outline.txt", replacementFile.Length));

        Assert.Contains("Confirm overwrite", ex.Message);
        Assert.Equal(1, await context.Documents.CountAsync(d => !d.IsDeleted));
    }

    [Fact]
    public async Task UploadDocumentAsync_OverwriteConfirmedSoftDeletesExistingFileName()
    {
        await using var context = CreateContext();
        var datasetId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedDatasetAndUser(context, datasetId, userId);

        var service = CreateService(context);
        var firstFile = NewTextStream("old content");
        await service.UploadDocumentAsync(datasetId, userId, firstFile, "outline.txt", firstFile.Length);

        var replacementFile = NewTextStream("new content");
        var replacement = await service.UploadDocumentAsync(datasetId, userId, replacementFile, "outline.txt", replacementFile.Length, overwriteExistingFileName: true);
        Assert.Equal(2, await context.Documents.CountAsync(d => !d.IsDeleted));

        await service.ProcessUploadedDocumentAsync(replacement.DocumentId);

        Assert.Equal(2, await context.Documents.CountAsync());
        Assert.Equal(1, await context.Documents.CountAsync(d => d.IsDeleted));
        Assert.Equal(1, await context.Documents.CountAsync(d => !d.IsDeleted && d.FileName == "outline.txt"));
    }

    [Fact]
    public async Task UploadDocumentAsync_AllowsSameFileAfterSoftDelete()
    {
        await using var context = CreateContext();
        var datasetId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedDatasetAndUser(context, datasetId, userId);

        var service = CreateService(context);
        var firstFile = NewTextStream("same content");
        var document = await service.UploadDocumentAsync(datasetId, userId, firstFile, "outline.txt", firstFile.Length);

        await service.DeleteDocumentAsync(document.DocumentId, userId);

        var replacementFile = NewTextStream("same content");
        await service.UploadDocumentAsync(datasetId, userId, replacementFile, "replacement.txt", replacementFile.Length);

        Assert.Equal(2, await context.Documents.CountAsync());
        Assert.Equal(1, await context.Documents.CountAsync(d => !d.IsDeleted));
    }

    [Fact]
    public async Task GetDocumentsByDatasetAsync_ReturnsOnlyActiveDocuments()
    {
        await using var context = CreateContext();
        var datasetId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedDatasetAndUser(context, datasetId, userId);

        context.Documents.AddRange(
            NewDocument(datasetId, userId, "active.txt", "Completed", isDeleted: false),
            NewDocument(datasetId, userId, "deleted.txt", "Deleted", isDeleted: true));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var documents = await service.GetDocumentsByDatasetAsync(datasetId);

        var document = Assert.Single(documents);
        Assert.Equal("active.txt", document.FileName);
    }

    [Fact]
    public async Task HasCompletedDocumentsAsync_RequiresActiveCompletedDocument()
    {
        await using var context = CreateContext();
        var datasetId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedDatasetAndUser(context, datasetId, userId);

        context.Documents.AddRange(
            NewDocument(datasetId, userId, "failed.txt", "Failed", isDeleted: false),
            NewDocument(datasetId, userId, "deleted.txt", "Completed", isDeleted: true));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        Assert.False(await service.HasCompletedDocumentsAsync(datasetId));

        context.Documents.Add(NewDocument(datasetId, userId, "active.txt", "Completed", isDeleted: false));
        await context.SaveChangesAsync();

        Assert.True(await service.HasCompletedDocumentsAsync(datasetId));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new TestAppDbContext(options);
    }

    private static DocumentService CreateService(AppDbContext context)
    {
        return new DocumentService(
            new UnitOfWork(context),
            new FakeRagApiClient(),
            new FakeFileStorageService(),
            new FakeRealtimeService(),
            new FakeSystemSettingService(),
            NullLogger<DocumentService>.Instance);
    }

    private static void SeedDatasetAndUser(AppDbContext context, Guid datasetId, Guid userId)
    {
        context.Users.Add(new User
        {
            UserId = userId,
            FullName = "Teacher",
            Email = "teacher@example.edu",
            Username = "teacher",
            PasswordHash = "hash",
            Role = "Teacher",
            IsApproved = true
        });

        context.Datasets.Add(new Dataset
        {
            DatasetId = datasetId,
            Name = "PRN222",
            CreatedBy = userId,
            IsApproved = true,
            IsPublic = true
        });

        context.SaveChanges();
    }

    private static Document NewDocument(Guid datasetId, Guid userId, string fileName, string status, bool isDeleted)
    {
        return new Document
        {
            DocumentId = Guid.NewGuid(),
            DatasetId = datasetId,
            FileName = fileName,
            FilePath = fileName,
            FileType = "txt",
            FileSize = 12,
            FileHash = Guid.NewGuid().ToString("N"),
            Status = status,
            UploadedBy = userId,
            UploadedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTime.UtcNow : null,
            DeletedBy = isDeleted ? userId : null
        };
    }

    private static MemoryStream NewTextStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public Task DeleteFileIfExistsAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream>(NewTextStream("stored content"));
        }

        public Task<StoredFileDto> SaveDatasetFileAsync(Guid datasetId, Stream fileStream, string fileName, long fileSize, CancellationToken cancellationToken = default)
        {
            var fileType = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
            return Task.FromResult(new StoredFileDto(fileName, fileName, fileName, fileType, fileSize));
        }
    }

    private sealed class FakeRagApiClient : IRagApiClient
    {
        public Task<bool> DeleteDocumentAsync(Guid documentId)
        {
            return Task.FromResult(true);
        }

        public Task<IndexResponseDto> IndexDocumentsAsync(IndexRequestDto request)
        {
            return Task.FromResult(new IndexResponseDto
            {
                Message = "ok",
                Embeddings = request.Documents.Select(_ => new float[384]).ToList()
            });
        }

        public Task<RetrieveResponseDto> RetrieveAsync(RetrieveRequestDto request)
        {
            return Task.FromResult(new RetrieveResponseDto());
        }
    }

    private sealed class FakeRealtimeService : IRealtimeService
    {
        public Task SendChatChunkAsync(Guid sessionId, Guid messageId, string chunk, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendChatCompleteAsync(Guid sessionId, ChatMessageDto assistantMessage, IReadOnlyList<CitationDto> citations, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendDocumentProgressAsync(Guid datasetId, Guid documentId, string status, int progressPercentage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendNotificationAsync(Guid userId, string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TriggerUiUpdateAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSystemSettingService : ISystemSettingService
    {
        public Task<int> GetChunkOverlapAsync(CancellationToken cancellationToken = default) => Task.FromResult(100);
        public Task<int> GetChunkSizeAsync(CancellationToken cancellationToken = default) => Task.FromResult(500);
        public Task<int> GetDailyTokenLimitAsync(CancellationToken cancellationToken = default) => Task.FromResult(50000);
        public Task UpdateSettingsAsync(int chunkSize, int chunkOverlap, int dailyTokenLimit, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<VectorRecord>().Ignore(v => v.Embedding);
        }
    }
}
