using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Tests;

public sealed class DatasetArchivalTests
{
    [Fact]
    public async Task ArchiveDataset_PreservesSessionsMessagesAndCitations_AndBlocksActiveAccess()
    {
        await using var context = CreateContext();
        var ids = SeedGraph(context);
        var service = new DatasetService(new UnitOfWork(context), new Mock<IRealtimeService>().Object);

        var archived = await service.ArchiveDatasetAsync(ids.DatasetId, true, ids.AdminId);

        Assert.True(archived);
        var dataset = await context.Datasets.SingleAsync();
        Assert.True(dataset.IsArchived);
        Assert.Equal(ids.AdminId, dataset.ArchivedBy);
        Assert.NotNull(dataset.ArchivedAt);
        Assert.Equal(1, await context.ChatSessions.CountAsync());
        Assert.Equal(1, await context.ChatMessages.CountAsync());
        Assert.Equal(1, await context.Citations.CountAsync());
        Assert.False(await service.CanAccessActiveDatasetAsync(ids.StudentId, "Student", ids.DatasetId));
        Assert.Empty(await service.GetDatasetsForUserAsync(ids.StudentId, "Student"));
        Assert.Empty(await service.GetAccessibleDatasetIdsAsync(ids.StudentId, "Student"));
    }

    [Fact]
    public async Task RestoreDataset_MakesItActiveAgain()
    {
        await using var context = CreateContext();
        var ids = SeedGraph(context);
        var service = new DatasetService(new UnitOfWork(context), new Mock<IRealtimeService>().Object);
        await service.ArchiveDatasetAsync(ids.DatasetId, true, ids.AdminId);

        var restored = await service.ArchiveDatasetAsync(ids.DatasetId, false, ids.AdminId);

        Assert.True(restored);
        Assert.True(await service.CanAccessActiveDatasetAsync(ids.StudentId, "Student", ids.DatasetId));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(options);
    }

    private static (Guid DatasetId, Guid AdminId, Guid StudentId) SeedGraph(AppDbContext context)
    {
        var adminId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var datasetId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        context.Users.AddRange(
            new User { UserId = adminId, FullName = "Admin", Email = $"{adminId}@test.local", Username = adminId.ToString(), PasswordHash = "hash", Role = "Admin", IsApproved = true },
            new User { UserId = studentId, FullName = "Student", Email = $"{studentId}@test.local", Username = studentId.ToString(), PasswordHash = "hash", Role = "Student", IsApproved = true });
        context.Datasets.Add(new Dataset { DatasetId = datasetId, Name = "Subject", CreatedBy = adminId, IsPublic = true, IsApproved = true });
        context.Documents.Add(new Document { DocumentId = documentId, DatasetId = datasetId, UploadedBy = adminId, FileName = "doc.txt", FilePath = "doc.txt", FileType = "txt", FileHash = "hash", Status = "Completed" });
        context.Chunks.Add(new Chunk { ChunkId = chunkId, DatasetId = datasetId, DocumentId = documentId, Content = "Evidence" });
        context.ChatSessions.Add(new ChatSession { SessionId = sessionId, DatasetId = datasetId, UserId = studentId, Title = "History" });
        context.ChatMessages.Add(new ChatMessage { MessageId = messageId, SessionId = sessionId, Role = "Assistant", Content = "Answer" });
        context.Citations.Add(new Citation { CitationId = Guid.NewGuid(), MessageId = messageId, DocumentId = documentId, ChunkId = chunkId, QuoteText = "Evidence", SourceLabel = "doc.txt" });
        context.SaveChanges();
        return (datasetId, adminId, studentId);
    }

    private sealed class TestAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<VectorRecord>().Ignore(v => v.Embedding);
        }
    }
}
