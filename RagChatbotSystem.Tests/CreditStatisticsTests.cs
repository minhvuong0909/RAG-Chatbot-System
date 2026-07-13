using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Tests;

public class CreditStatisticsTests
{
    [Fact]
    public async Task GetCreditReportAsync_AggregatesSpendAndBlockedAttempts()
    {
        await using var context = CreateContext();
        var userId = SeedStudent(context);
        var datasetId = SeedDataset(context, userId);
        context.CreditLedgers.AddRange(
            new CreditLedger
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DatasetId = datasetId,
                Type = CreditLedgerType.SPEND,
                CalculatedCredits = 5,
                ChargedCredits = 3,
                FreeCreditsUsed = 2,
                PaidCreditsUsed = 1,
                BalanceBeforeFree = 2,
                BalanceBeforePaid = 1,
                BalanceAfterFree = 0,
                BalanceAfterPaid = 0,
                WasInsufficientBalance = true,
                ModelName = "test-model",
                CreatedAt = DateTime.UtcNow
            },
            new CreditLedger
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DatasetId = datasetId,
                Type = CreditLedgerType.SPEND,
                CalculatedCredits = 1,
                ChargedCredits = 1,
                FreeCreditsUsed = 1,
                PaidCreditsUsed = 0,
                BalanceBeforeFree = 10,
                BalanceBeforePaid = 0,
                BalanceAfterFree = 9,
                BalanceAfterPaid = 0,
                ModelName = "test-model",
                CreatedAt = DateTime.UtcNow
            });
        context.CreditBlockedAttempts.Add(new CreditBlockedAttempt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DatasetId = datasetId,
            Reason = CreditBlockedReason.ZERO_BALANCE,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new StatisticsService(new UnitOfWork(context));
        var report = await service.GetCreditReportAsync(7);

        Assert.Equal(6, report.Summary.TotalCalculatedCredits);
        Assert.Equal(4, report.Summary.TotalChargedCredits);
        Assert.Equal(3, report.Summary.FreeCreditsConsumed);
        Assert.Equal(1, report.Summary.PaidCreditsConsumed);
        Assert.Equal(1, report.Summary.InsufficientBalanceCount);
        Assert.Equal(1, report.Summary.ZeroBalanceBlockedCount);
        Assert.Contains(report.TopModels, m => m.ModelName == "test-model" && m.ChargedCredits == 4);
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

    private static Guid SeedDataset(AppDbContext context, Guid userId)
    {
        var datasetId = Guid.NewGuid();
        context.Datasets.Add(new Dataset
        {
            DatasetId = datasetId,
            Name = "PRN222",
            CreatedBy = userId,
            IsApproved = true
        });
        context.SaveChanges();
        return datasetId;
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
