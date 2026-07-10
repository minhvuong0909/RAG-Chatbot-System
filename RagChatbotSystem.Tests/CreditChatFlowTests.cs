using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Tests;

public class CreditChatFlowTests
{
    [Fact]
    public async Task SendChatMessageAsync_ZeroBalanceBlocksBeforeRagAndLlm()
    {
        await using var context = CreateContext();
        var ids = SeedChatReadyStudent(context, freeCredits: 0, paidCredits: 0);
        var rag = new Mock<IRagApiClient>(MockBehavior.Strict);
        var llm = new Mock<ILlmService>(MockBehavior.Strict);
        var service = CreateChatService(context, rag.Object, llm.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendChatMessageAsync(ids.SessionId, "What is dependency injection?"));

        rag.Verify(r => r.RetrieveAsync(It.IsAny<RetrieveRequestDto>()), Times.Never);
        llm.Verify(l => l.GenerateAnswerWithUsageAsync(It.IsAny<string>()), Times.Never);
        Assert.Single(context.CreditBlockedAttempts.Where(a => a.Reason == CreditBlockedReason.ZERO_BALANCE));
        Assert.Empty(context.CreditLedgers.Where(l => l.Type == CreditLedgerType.SPEND));
    }

    [Fact]
    public async Task SendChatMessageAsync_ProviderFallbackDoesNotDeductCredits()
    {
        await using var context = CreateContext();
        var ids = SeedChatReadyStudent(context, freeCredits: 60, paidCredits: 0);
        var rag = new Mock<IRagApiClient>();
        rag.Setup(r => r.RetrieveAsync(It.IsAny<RetrieveRequestDto>()))
            .ReturnsAsync(new RetrieveResponseDto
            {
                Documents = new List<DocumentModelDto>
                {
                    new()
                    {
                        PageContent = "Dependency injection helps manage dependencies in PRN222.",
                        Metadata = new Dictionary<string, object>
                        {
                            ["dataset_id"] = ids.DatasetId.ToString(),
                            ["id"] = ids.ChunkId.ToString(),
                            ["document_id"] = ids.DocumentId.ToString(),
                            ["page_number"] = 1,
                            ["file_name"] = "lesson.txt"
                        }
                    }
                }
            });
        var llm = new Mock<ILlmService>();
        llm.Setup(l => l.GenerateAnswerWithUsageAsync(It.IsAny<string>()))
            .ReturnsAsync(LlmAnswerResult.Fallback(
                "Provider fallback answer",
                "test-model",
                100,
                20,
                "provider failed"));
        var service = CreateChatService(context, rag.Object, llm.Object);

        await service.SendChatMessageAsync(ids.SessionId, "dependency injection PRN222");

        var wallet = await context.CreditWallets.SingleAsync(w => w.UserId == ids.UserId);
        Assert.Equal(60, wallet.FreeCredits);
        Assert.Empty(context.CreditLedgers.Where(l => l.Type == CreditLedgerType.SPEND));
        Assert.Single(context.CreditBlockedAttempts.Where(a => a.Reason == CreditBlockedReason.PROVIDER_ERROR));
    }

    [Fact]
    public async Task SendChatMessageAsync_CreditSystemDisabled_AllowsZeroBalanceAndDoesNotSpend()
    {
        await using var context = CreateContext();
        DisableCreditSystem(context);
        var ids = SeedChatReadyStudent(context, freeCredits: 0, paidCredits: 0);
        var rag = CreateSuccessfulRag(ids);
        var llm = new Mock<ILlmService>();
        llm.Setup(l => l.GenerateAnswerWithUsageAsync(It.IsAny<string>()))
            .ReturnsAsync(new LlmAnswerResult(
                "Dependency injection answer",
                "test-model",
                100,
                20,
                120,
                true,
                true,
                false,
                null));
        var service = CreateChatService(context, rag.Object, llm.Object);

        await service.SendChatMessageAsync(ids.SessionId, "dependency injection PRN222");

        llm.Verify(l => l.GenerateAnswerWithUsageAsync(It.IsAny<string>()), Times.Once);
        var wallet = await context.CreditWallets.SingleAsync(w => w.UserId == ids.UserId);
        Assert.Equal(0, wallet.FreeCredits);
        Assert.Equal(0, wallet.PaidCredits);
        Assert.Empty(context.CreditLedgers.Where(l => l.Type == CreditLedgerType.SPEND));
        Assert.Empty(context.CreditBlockedAttempts.Where(a => a.Reason == CreditBlockedReason.ZERO_BALANCE));
    }

    [Fact]
    public async Task SendChatMessageAsync_DailyTokenLimitBlock_DoesNotDeductCredits()
    {
        await using var context = CreateContext();
        var ids = SeedChatReadyStudent(context, freeCredits: 60, paidCredits: 10);
        var today = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ResolveVietnamTimeZone()).Date, DateTimeKind.Utc);
        context.UserTokenUsages.Add(new UserTokenUsage
        {
            Id = Guid.NewGuid(),
            UserId = ids.UserId,
            DatasetId = ids.DatasetId,
            Date = today,
            TokenCount = 50000,
            QueryCount = 1
        });
        await context.SaveChangesAsync();
        var rag = new Mock<IRagApiClient>(MockBehavior.Strict);
        var llm = new Mock<ILlmService>(MockBehavior.Strict);
        var service = CreateChatService(context, rag.Object, llm.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendChatMessageAsync(ids.SessionId, "dependency injection PRN222"));

        rag.Verify(r => r.RetrieveAsync(It.IsAny<RetrieveRequestDto>()), Times.Never);
        llm.Verify(l => l.GenerateAnswerWithUsageAsync(It.IsAny<string>()), Times.Never);
        var wallet = await context.CreditWallets.SingleAsync(w => w.UserId == ids.UserId);
        Assert.Equal(60, wallet.FreeCredits);
        Assert.Equal(10, wallet.PaidCredits);
        Assert.Empty(context.CreditLedgers.Where(l => l.Type == CreditLedgerType.SPEND));
        Assert.Single(context.CreditBlockedAttempts.Where(a => a.Reason == CreditBlockedReason.DAILY_TOKEN_LIMIT));
    }

    [Fact]
    public async Task SendChatMessageAsync_SendsRealtimeOnlyAfterAssistantIsPersisted()
    {
        await using var context = CreateContext();
        var ids = SeedChatReadyStudent(context, freeCredits: 60, paidCredits: 0);
        var rag = CreateSuccessfulRag(ids);
        var llm = new Mock<ILlmService>();
        llm.Setup(l => l.GenerateAnswerWithUsageAsync(It.IsAny<string>()))
            .ReturnsAsync(new LlmAnswerResult(
                "Dependency injection answer",
                "test-model",
                100,
                20,
                120,
                true,
                true,
                false,
                null));
        var realtime = new Mock<IRealtimeService>();
        realtime.Setup(r => r.SendChatChunkAsync(ids.SessionId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, CancellationToken>((_, messageId, _, _) =>
            {
                Assert.True(context.ChatMessages.Any(m => m.MessageId == messageId && m.Role == "Assistant"));
            })
            .Returns(Task.CompletedTask);
        realtime.Setup(r => r.SendChatCompleteAsync(ids.SessionId, It.IsAny<ChatMessageDto>(), It.IsAny<IReadOnlyList<CitationDto>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, ChatMessageDto, IReadOnlyList<CitationDto>, CancellationToken>((_, message, _, _) =>
            {
                Assert.True(context.ChatMessages.Any(m => m.MessageId == message.MessageId && m.Role == "Assistant"));
            })
            .Returns(Task.CompletedTask);
        var service = CreateChatService(context, rag.Object, llm.Object, realtime.Object);

        await service.SendChatMessageAsync(ids.SessionId, "dependency injection PRN222");

        realtime.Verify(r => r.SendChatChunkAsync(ids.SessionId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        realtime.Verify(r => r.SendChatCompleteAsync(ids.SessionId, It.IsAny<ChatMessageDto>(), It.IsAny<IReadOnlyList<CitationDto>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ChatService CreateChatService(AppDbContext context, IRagApiClient rag, ILlmService llm, IRealtimeService? realtime = null)
    {
        var unitOfWork = new UnitOfWork(context);
        return new ChatService(
            unitOfWork,
            rag,
            llm,
            realtime ?? Mock.Of<IRealtimeService>(),
            new TokenUsageService(unitOfWork),
            new CreditService(context),
            NullLogger<ChatService>.Instance);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new TestAppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static Mock<IRagApiClient> CreateSuccessfulRag(ChatSeedIds ids)
    {
        var rag = new Mock<IRagApiClient>();
        rag.Setup(r => r.RetrieveAsync(It.IsAny<RetrieveRequestDto>()))
            .ReturnsAsync(new RetrieveResponseDto
            {
                Documents = new List<DocumentModelDto>
                {
                    new()
                    {
                        PageContent = "Dependency injection helps manage dependencies in PRN222.",
                        Metadata = new Dictionary<string, object>
                        {
                            ["dataset_id"] = ids.DatasetId.ToString(),
                            ["id"] = ids.ChunkId.ToString(),
                            ["document_id"] = ids.DocumentId.ToString(),
                            ["page_number"] = 1,
                            ["file_name"] = "lesson.txt"
                        }
                    }
                }
            });
        return rag;
    }

    private static void DisableCreditSystem(AppDbContext context)
    {
        var setting = context.SystemSettings.Single();
        setting.EnableCreditSystem = false;
        context.SaveChanges();
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch {}
        }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");
    }

    private static ChatSeedIds SeedChatReadyStudent(AppDbContext context, int freeCredits, int paidCredits)
    {
        var userId = Guid.NewGuid();
        var datasetId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        context.Users.Add(new User
        {
            UserId = userId,
            FullName = "Student",
            Email = $"{userId:N}@example.edu",
            Username = $"student-{userId:N}",
            PasswordHash = "hash",
            Role = "Student",
            IsApproved = true
        });
        context.Datasets.Add(new Dataset
        {
            DatasetId = datasetId,
            Name = "PRN222",
            CreatedBy = userId,
            IsApproved = true
        });
        context.Documents.Add(new Document
        {
            DocumentId = documentId,
            DatasetId = datasetId,
            FileName = "lesson.txt",
            FilePath = "lesson.txt",
            FileType = "txt",
            FileSize = 100,
            Status = "Completed",
            UploadedBy = userId,
            IsDeleted = false
        });
        context.Chunks.Add(new Chunk
        {
            ChunkId = chunkId,
            DatasetId = datasetId,
            DocumentId = documentId,
            ChunkIndex = 0,
            Content = "Dependency injection helps manage dependencies in PRN222.",
            PageNumber = 1
        });
        context.ChatSessions.Add(new ChatSession
        {
            SessionId = sessionId,
            UserId = userId,
            DatasetId = datasetId,
            Title = "Test"
        });
        context.CreditWallets.Add(new CreditWallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FreeCredits = freeCredits,
            PaidCredits = paidCredits,
            LastFreeCreditResetDate = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ResolveVietnamTimeZone()).Date, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        });
        context.SaveChanges();
        return new ChatSeedIds(userId, datasetId, sessionId, documentId, chunkId);
    }

    private sealed record ChatSeedIds(Guid UserId, Guid DatasetId, Guid SessionId, Guid DocumentId, Guid ChunkId);

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
