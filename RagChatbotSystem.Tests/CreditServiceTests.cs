using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Tests;

public class CreditServiceTests
{
    [Fact]
    public async Task CalculateCreditsAsync_UsesDefaultWeightedFormulaAndMinimum()
    {
        await using var context = CreateContext();
        var service = new CreditService(context);

        Assert.Equal(1, await service.CalculateCreditsAsync(1, 0));
        Assert.Equal(3, await service.CalculateCreditsAsync(1000, 500));
    }

    [Fact]
    public async Task GetOrCreateBalanceAsync_CreatesWalletWithDailyResetLedger()
    {
        await using var context = CreateContext();
        var userId = SeedStudent(context);
        var service = new CreditService(context);

        var balance = await service.GetOrCreateBalanceAsync(userId);

        Assert.Equal(60, balance.FreeCredits);
        Assert.Equal(0, balance.PaidCredits);
        var ledger = Assert.Single(context.CreditLedgers);
        Assert.Equal(CreditLedgerType.DAILY_RESET, ledger.Type);
        Assert.Equal(60, ledger.FreeCreditsAdded);
    }

    [Fact]
    public async Task GetOrCreateBalanceAsync_RepeatedFirstAccess_DoesNotDuplicateWallet()
    {
        await using var context = CreateContext();
        var userId = SeedStudent(context);
        var service = new CreditService(context);

        await service.GetOrCreateBalanceAsync(userId);
        await service.GetOrCreateBalanceAsync(userId);

        Assert.Single(context.CreditWallets.Where(w => w.UserId == userId));
        Assert.Single(context.CreditLedgers.Where(l => l.UserId == userId && l.Type == CreditLedgerType.DAILY_RESET));
    }

    [Fact]
    public async Task EnsureDailyFreeCreditsAsync_ResetsToFixedAmountWithoutRollover()
    {
        await using var context = CreateContext();
        var userId = SeedStudent(context);
        context.CreditWallets.Add(new CreditWallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FreeCredits = 10,
            PaidCredits = 5,
            LastFreeCreditResetDate = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1).Date, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            Version = 1
        });
        await context.SaveChangesAsync();
        var service = new CreditService(context);

        var balance = await service.EnsureDailyFreeCreditsAsync(userId);

        Assert.Equal(60, balance.FreeCredits);
        Assert.Equal(5, balance.PaidCredits);
        Assert.Contains(context.CreditLedgers, l => l.Type == CreditLedgerType.DAILY_RESET && l.BalanceAfterFree == 60 && l.FreeCreditsAdded == 50);
    }

    [Fact]
    public async Task SpendForChatAnswerAsync_DeductsFreeCreditsFirst()
    {
        await using var context = CreateContext();
        var ids = SeedChatContext(context, freeCredits: 60, paidCredits: 100);
        var service = new CreditService(context);

        var result = await service.SpendForChatAnswerAsync(ids.UserId, ids.DatasetId, ids.SessionId, ids.MessageId, 1000, 500, 1500, "test-model", true);

        Assert.Equal(3, result.CalculatedCredits);
        Assert.Equal(3, result.ChargedCredits);
        Assert.Equal(3, result.FreeCreditsUsed);
        Assert.Equal(0, result.PaidCreditsUsed);
        Assert.Equal(57, result.BalanceAfterFree);
        Assert.Equal(100, result.BalanceAfterPaid);
    }

    [Fact]
    public async Task SpendForChatAnswerAsync_DeductsFreeThenPaid()
    {
        await using var context = CreateContext();
        var ids = SeedChatContext(context, freeCredits: 2, paidCredits: 10);
        var service = new CreditService(context);

        var result = await service.SpendForChatAnswerAsync(ids.UserId, ids.DatasetId, ids.SessionId, ids.MessageId, 1000, 500, 1500, "test-model", true);

        Assert.Equal(3, result.ChargedCredits);
        Assert.Equal(2, result.FreeCreditsUsed);
        Assert.Equal(1, result.PaidCreditsUsed);
        Assert.Equal(0, result.BalanceAfterFree);
        Assert.Equal(9, result.BalanceAfterPaid);
    }

    [Fact]
    public async Task SpendForChatAnswerAsync_DeductsPaidOnlyWhenFreeIsZero()
    {
        await using var context = CreateContext();
        var ids = SeedChatContext(context, freeCredits: 0, paidCredits: 10);
        var service = new CreditService(context);

        var result = await service.SpendForChatAnswerAsync(ids.UserId, ids.DatasetId, ids.SessionId, ids.MessageId, 1000, 500, 1500, "test-model", false);

        Assert.Equal(0, result.FreeCreditsUsed);
        Assert.Equal(3, result.PaidCreditsUsed);
        Assert.Equal(7, result.BalanceAfterPaid);
        Assert.False(result.WasActualTokenUsage);
    }

    [Fact]
    public async Task SpendForChatAnswerAsync_InsufficientBalanceSetsBalancesToZero()
    {
        await using var context = CreateContext();
        var ids = SeedChatContext(context, freeCredits: 1, paidCredits: 1);
        var service = new CreditService(context);

        var result = await service.SpendForChatAnswerAsync(ids.UserId, ids.DatasetId, ids.SessionId, ids.MessageId, 1000, 500, 1500, "test-model", true);

        Assert.Equal(3, result.CalculatedCredits);
        Assert.Equal(2, result.ChargedCredits);
        Assert.True(result.WasInsufficientBalance);
        Assert.Equal(0, result.BalanceAfterFree);
        Assert.Equal(0, result.BalanceAfterPaid);
        Assert.DoesNotContain(context.CreditWallets, w => w.FreeCredits < 0 || w.PaidCredits < 0);
    }

    [Fact]
    public async Task CanStudentAskAsync_ReturnsFalseWhenTotalBalanceIsZero()
    {
        await using var context = CreateContext();
        var userId = SeedStudent(context);
        context.CreditWallets.Add(new CreditWallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FreeCredits = 0,
            PaidCredits = 0,
            LastFreeCreditResetDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        });
        await context.SaveChangesAsync();
        var service = new CreditService(context);

        Assert.False(await service.CanStudentAskAsync(userId));
    }

    [Fact]
    public async Task LogBlockedAttemptAsync_SavesZeroBalanceAttempt()
    {
        await using var context = CreateContext();
        var userId = SeedStudent(context);
        var service = new CreditService(context);

        await service.LogBlockedAttemptAsync(userId, CreditBlockedReason.ZERO_BALANCE, messagePreview: "hello");

        var attempt = Assert.Single(context.CreditBlockedAttempts);
        Assert.Equal(CreditBlockedReason.ZERO_BALANCE, attempt.Reason);
        Assert.Equal("hello", attempt.MessagePreview);
    }

    [Fact]
    public async Task GrantAndAdjustment_CreateLedgerRows()
    {
        await using var context = CreateContext();
        var userId = SeedStudent(context);
        var adminId = SeedAdmin(context);
        var service = new CreditService(context);

        await service.GrantFreeCreditsAsync(userId, 5, adminId, "bonus");
        await service.AdjustCreditsAsync(userId, -3, 7, adminId, "correction");

        Assert.Contains(context.CreditLedgers, l => l.Type == CreditLedgerType.FREE_GRANT && l.FreeCreditsAdded == 5);
        Assert.Contains(context.CreditLedgers, l => l.Type == CreditLedgerType.ADJUSTMENT && l.PaidCreditsAdded == 7);
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

    private static Guid SeedStudent(AppDbContext context)
    {
        var userId = Guid.NewGuid();
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
        context.SaveChanges();
        return userId;
    }

    private static Guid SeedAdmin(AppDbContext context)
    {
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            UserId = userId,
            FullName = "Admin",
            Email = $"{userId:N}@example.edu",
            Username = $"admin-{userId:N}",
            PasswordHash = "hash",
            Role = "Admin",
            IsApproved = true
        });
        context.SaveChanges();
        return userId;
    }

    private static ChatIds SeedChatContext(AppDbContext context, int freeCredits, int paidCredits)
    {
        var userId = SeedStudent(context);
        var datasetId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        context.Datasets.Add(new Dataset
        {
            DatasetId = datasetId,
            Name = "PRN222",
            CreatedBy = userId,
            IsApproved = true
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
            LastFreeCreditResetDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        });
        context.SaveChanges();
        return new ChatIds(userId, datasetId, sessionId, messageId);
    }

    private sealed record ChatIds(Guid UserId, Guid DatasetId, Guid SessionId, Guid MessageId);

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
