using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Exceptions;
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

        var exception = await Assert.ThrowsAsync<ChatRequestBlockedException>(() =>
            service.SendChatMessageAsync(ids.SessionId, "What is dependency injection?"));

        Assert.Equal(ChatBlockReason.InsufficientCredits, exception.Reason);
        Assert.Equal("Bạn đã hết Credit. Vui lòng nạp thêm Credit để tiếp tục đặt câu hỏi.", exception.Message);

        rag.Verify(r => r.RetrieveAsync(It.IsAny<RetrieveRequestDto>()), Times.Never);
        llm.Verify(l => l.GenerateAnswerStreamAsync(It.IsAny<string>()), Times.Never);
        Assert.Single(context.CreditBlockedAttempts.Where(a => a.Reason == CreditBlockedReason.ZERO_BALANCE));
        Assert.Empty(context.CreditLedgers.Where(l => l.Type == CreditLedgerType.SPEND));
    }

    [Fact]
    public async Task SendChatMessageAsync_ProviderFallbackFailsWithoutPersistenceOrUsage()
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
        SetupLlmStream(llm, new[] { "Provider ", "fallback answer" }, isProviderFallback: true, errorMessage: "provider failed");
        var realtime = new Mock<IRealtimeService>();
        realtime.Setup(r => r.SendChatChunkAsync(ids.SessionId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        realtime.Setup(r => r.SendChatFailedAsync(ids.SessionId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateChatService(context, rag.Object, llm.Object, realtime.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendChatMessageAsync(ids.SessionId, "dependency injection PRN222"));

        Assert.Contains("Hiện chưa thể tạo câu trả lời từ AI", exception.Message);
        var wallet = await context.CreditWallets.SingleAsync(w => w.UserId == ids.UserId);
        Assert.Equal(60, wallet.FreeCredits);
        Assert.Empty(context.CreditLedgers.Where(l => l.Type == CreditLedgerType.SPEND));
        Assert.Single(context.CreditBlockedAttempts.Where(a => a.Reason == CreditBlockedReason.PROVIDER_ERROR));
        Assert.Empty(context.ChatMessages.Where(m => m.Role == "Assistant"));
        Assert.Empty(context.Citations);
        Assert.Empty(context.UserTokenUsages);
        realtime.Verify(r => r.SendChatFailedAsync(ids.SessionId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        realtime.Verify(r => r.SendChatCompleteAsync(
            ids.SessionId,
            It.IsAny<ChatMessageDto>(),
            It.IsAny<IReadOnlyList<CitationDto>>(),
            It.IsAny<CreditSpendResultDto?>(),
            It.IsAny<CreditBalanceDto?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendChatMessageAsync_ProviderErrorSendsFailureWithoutFakeChunkOrUsage()
    {
        await using var context = CreateContext();
        var ids = SeedChatReadyStudent(context, freeCredits: 60, paidCredits: 0);
        var rag = CreateSuccessfulRag(ids);
        var llm = new Mock<ILlmService>();
        llm.Setup(l => l.GenerateAnswerStreamAsync(It.IsAny<string>()))
            .Returns(new ThrowingAsyncEnumerable("Groq API 401 Unauthorized"));
        llm.SetupGet(l => l.ModelName).Returns("llama-test");
        var realtime = new Mock<IRealtimeService>();
        realtime.Setup(r => r.SendChatFailedAsync(ids.SessionId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateChatService(context, rag.Object, llm.Object, realtime.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendChatMessageAsync(ids.SessionId, "dependency injection PRN222"));

        Assert.Contains("Hiện chưa thể tạo câu trả lời từ AI", exception.Message);
        realtime.Verify(r => r.SendChatChunkAsync(ids.SessionId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        realtime.Verify(r => r.SendChatFailedAsync(ids.SessionId, It.IsAny<Guid>(), It.Is<string>(message => message.Contains("Hiện chưa thể tạo câu trả lời từ AI")), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(context.ChatMessages.Where(m => m.Role == "Assistant"));
        Assert.Empty(context.Citations);
        Assert.Empty(context.CreditLedgers.Where(l => l.Type == CreditLedgerType.SPEND));
        Assert.Empty(context.UserTokenUsages);
        Assert.Single(context.CreditBlockedAttempts.Where(a => a.Reason == CreditBlockedReason.PROVIDER_ERROR));
        realtime.Verify(r => r.SendChatCompleteAsync(
            ids.SessionId,
            It.IsAny<ChatMessageDto>(),
            It.IsAny<IReadOnlyList<CitationDto>>(),
            It.IsAny<CreditSpendResultDto?>(),
            It.IsAny<CreditBalanceDto?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendChatMessageAsync_CreditSystemDisabled_AllowsZeroBalanceAndDoesNotSpend()
    {
        await using var context = CreateContext();
        DisableCreditSystem(context);
        var ids = SeedChatReadyStudent(context, freeCredits: 0, paidCredits: 0);
        var rag = CreateSuccessfulRag(ids);
        var llm = new Mock<ILlmService>();
        SetupLlmStream(llm, new[] { "Dependency ", "injection answer" });
        var service = CreateChatService(context, rag.Object, llm.Object);

        await service.SendChatMessageAsync(ids.SessionId, "dependency injection PRN222");

        llm.Verify(l => l.GenerateAnswerStreamAsync(It.IsAny<string>()), Times.Once);
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

        var exception = await Assert.ThrowsAsync<ChatRequestBlockedException>(() =>
            service.SendChatMessageAsync(ids.SessionId, "dependency injection PRN222"));

        Assert.Equal(ChatBlockReason.DailyTokenLimit, exception.Reason);

        rag.Verify(r => r.RetrieveAsync(It.IsAny<RetrieveRequestDto>()), Times.Never);
        llm.Verify(l => l.GenerateAnswerStreamAsync(It.IsAny<string>()), Times.Never);
        var wallet = await context.CreditWallets.SingleAsync(w => w.UserId == ids.UserId);
        Assert.Equal(60, wallet.FreeCredits);
        Assert.Equal(10, wallet.PaidCredits);
        Assert.Empty(context.CreditLedgers.Where(l => l.Type == CreditLedgerType.SPEND));
        Assert.Single(context.CreditBlockedAttempts.Where(a => a.Reason == CreditBlockedReason.DAILY_TOKEN_LIMIT));
    }

    [Fact]
    public async Task SendChatMessageAsync_StreamsChunksBeforeCompletionAndCompletesAfterPersistence()
    {
        await using var context = CreateContext();
        var ids = SeedChatReadyStudent(context, freeCredits: 60, paidCredits: 0);
        var rag = CreateSuccessfulRag(ids);
        var llm = new Mock<ILlmService>();
        SetupLlmStream(llm, new[] { "Dependency ", "injection answer" });
        var realtime = new Mock<IRealtimeService>();
        var chunkWasSentBeforePersistence = false;
        realtime.Setup(r => r.SendChatChunkAsync(ids.SessionId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, CancellationToken>((_, messageId, _, _) =>
            {
                chunkWasSentBeforePersistence = !context.ChatMessages.Any(m => m.MessageId == messageId && m.Role == "Assistant");
            })
            .Returns(Task.CompletedTask);
        realtime.Setup(r => r.SendChatCompleteAsync(
                ids.SessionId,
                It.IsAny<ChatMessageDto>(),
                It.IsAny<IReadOnlyList<CitationDto>>(),
                It.IsAny<CreditSpendResultDto?>(),
                It.IsAny<CreditBalanceDto?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, ChatMessageDto, IReadOnlyList<CitationDto>, CreditSpendResultDto?, CreditBalanceDto?, CancellationToken>((_, message, _, _, _, _) =>
            {
                Assert.True(context.ChatMessages.Any(m => m.MessageId == message.MessageId && m.Role == "Assistant"));
            })
            .Returns(Task.CompletedTask);
        var service = CreateChatService(context, rag.Object, llm.Object, realtime.Object);

        await service.SendChatMessageAsync(ids.SessionId, "dependency injection PRN222");

        Assert.True(chunkWasSentBeforePersistence);
        realtime.Verify(r => r.SendChatChunkAsync(ids.SessionId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        realtime.Verify(r => r.SendChatCompleteAsync(
            ids.SessionId,
            It.IsAny<ChatMessageDto>(),
            It.IsAny<IReadOnlyList<CitationDto>>(),
            It.IsAny<CreditSpendResultDto?>(),
            It.IsAny<CreditBalanceDto?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        realtime.Verify(r => r.SendCreditBalanceChangedAsync(
            ids.UserId,
            It.IsAny<CreditBalanceDto>(),
            "chat-spend",
            It.IsAny<CreditSpendResultDto?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendChatMessageAsync_PersistenceFailureAfterStreaming_SendsFailureAndDoesNotSpend()
    {
        await using var context = CreateFailingContext();
        var ids = SeedChatReadyStudent(context, freeCredits: 60, paidCredits: 0);
        var rag = CreateSuccessfulRag(ids);
        var llm = new Mock<ILlmService>();
        SetupLlmStream(llm, new[] { "Dependency ", "injection answer" });
        var realtime = new Mock<IRealtimeService>();
        realtime.Setup(r => r.SendChatChunkAsync(ids.SessionId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => context.FailSaves = true)
            .Returns(Task.CompletedTask);
        realtime.Setup(r => r.SendChatFailedAsync(ids.SessionId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateChatService(context, rag.Object, llm.Object, realtime.Object);

        await Assert.ThrowsAnyAsync<Exception>(() => service.SendChatMessageAsync(ids.SessionId, "dependency injection PRN222"));

        realtime.Verify(r => r.SendChatFailedAsync(ids.SessionId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(context.CreditLedgers.Where(l => l.Type == CreditLedgerType.SPEND));
    }

    [Fact]
    public async Task SendChatMessageAsync_DatasetOverviewCoversEveryActiveDocument()
    {
        await using var context = CreateContext();
        var ids = SeedChatReadyStudent(context, freeCredits: 60, paidCredits: 0);
        var secondDocumentId = AddCompletedDocumentWithChunk(
            context,
            ids,
            "registration.docx",
            "Registration rules and enrollment deadlines.");
        var thirdDocumentId = AddCompletedDocumentWithChunk(
            context,
            ids,
            "writing-guide.docx",
            "IELTS writing task two structure and assessment criteria.");

        // Simulate retrieval being dominated by one document, which is what the reported bug exposed.
        var rag = CreateSuccessfulRag(ids);
        var llm = new Mock<ILlmService>();
        string? capturedPrompt = null;
        SetupLlmStream(llm, new[] { "A synthesized overview across the uploaded documents." });
        llm.Setup(l => l.GenerateAnswerStreamAsync(It.IsAny<string>()))
            .Callback<string>(prompt => capturedPrompt = prompt)
            .Returns(StreamChunks(new[] { "A synthesized overview across the uploaded documents." }));
        var service = CreateChatService(context, rag.Object, llm.Object);

        var response = await service.SendChatMessageAsync(
            ids.SessionId,
            "Tom tat y chinh cua mon hoc nay theo tat ca tai lieu da tai len");

        Assert.NotNull(capturedPrompt);
        Assert.Contains("Dependency injection helps manage dependencies", capturedPrompt);
        Assert.Contains("Registration rules and enrollment deadlines", capturedPrompt);
        Assert.Contains("IELTS writing task two structure", capturedPrompt);
        Assert.Equal(3, response.Citations.Select(c => c.DocumentId).Distinct().Count());
        Assert.All(response.Citations, citation => Assert.Equal(0, citation.PageNumber));
        Assert.Contains(secondDocumentId, response.Citations.Select(c => c.DocumentId));
        Assert.Contains(thirdDocumentId, response.Citations.Select(c => c.DocumentId));
    }

    [Fact]
    public async Task SendChatMessageAsync_DoesNotReplaceModelAnswerWithRawChunks()
    {
        await using var context = CreateContext();
        var ids = SeedChatReadyStudent(context, freeCredits: 60, paidCredits: 0);
        var rag = CreateSuccessfulRag(ids);
        var llm = new Mock<ILlmService>();
        const string modelAnswer = "Toi khong tim thay thong tin nay trong tai lieu cua ban.";
        SetupLlmStream(llm, new[] { modelAnswer });
        var service = CreateChatService(context, rag.Object, llm.Object);

        var response = await service.SendChatMessageAsync(ids.SessionId, "dependency injection PRN222");

        Assert.Equal(modelAnswer, response.AssistantMessage.Content);
        Assert.DoesNotContain("cac y chinh co the rut ra", response.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
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

    private static Guid AddCompletedDocumentWithChunk(
        AppDbContext context,
        ChatSeedIds ids,
        string fileName,
        string content)
    {
        var documentId = Guid.NewGuid();
        context.Documents.Add(new Document
        {
            DocumentId = documentId,
            DatasetId = ids.DatasetId,
            FileName = fileName,
            FilePath = fileName,
            FileType = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
            FileSize = content.Length,
            Status = "Completed",
            UploadedBy = ids.UserId,
            IsDeleted = false
        });
        context.Chunks.Add(new Chunk
        {
            ChunkId = Guid.NewGuid(),
            DatasetId = ids.DatasetId,
            DocumentId = documentId,
            ChunkIndex = 1,
            Content = content,
            PageNumber = 0
        });
        context.SaveChanges();
        return documentId;
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

    private static FailingSaveAppDbContext CreateFailingContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new FailingSaveAppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static void SetupLlmStream(
        Mock<ILlmService> llm,
        IReadOnlyList<string> chunks,
        bool isProviderFallback = false,
        string? errorMessage = null)
    {
        llm.Setup(l => l.GenerateAnswerStreamAsync(It.IsAny<string>()))
            .Returns(StreamChunks(chunks));
        llm.SetupGet(l => l.LastPromptTokens).Returns(100);
        llm.SetupGet(l => l.LastCompletionTokens).Returns(20);
        llm.SetupGet(l => l.LastTotalTokens).Returns(120);
        llm.SetupGet(l => l.LastWasActualTokenUsage).Returns(!isProviderFallback);
        llm.SetupGet(l => l.LastIsProviderFallback).Returns(isProviderFallback);
        llm.SetupGet(l => l.LastErrorMessage).Returns(errorMessage);
        llm.SetupGet(l => l.ModelName).Returns("test-model");
    }

    private static async IAsyncEnumerable<string> StreamChunks(IReadOnlyList<string> chunks)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }

    private sealed class ThrowingAsyncEnumerable : IAsyncEnumerable<string>
    {
        private readonly string _message;

        public ThrowingAsyncEnumerable(string message)
        {
            _message = message;
        }

        public IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new ThrowingAsyncEnumerator(_message);
        }
    }

    private sealed class ThrowingAsyncEnumerator : IAsyncEnumerator<string>
    {
        private readonly string _message;

        public ThrowingAsyncEnumerator(string message)
        {
            _message = message;
        }

        public string Current => string.Empty;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<bool> MoveNextAsync()
        {
            throw new InvalidOperationException(_message);
        }
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

    private class TestAppDbContext : AppDbContext
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

    private sealed class FailingSaveAppDbContext : TestAppDbContext
    {
        public bool FailSaves { get; set; }

        public FailingSaveAppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailSaves)
            {
                throw new InvalidOperationException("Simulated persistence failure.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
