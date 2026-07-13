using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Tests;

public class CreditPurchaseServiceTests
{
    [Fact]
    public async Task GetPackagesAsync_ReturnsSeededDefaultPackages()
    {
        await using var context = CreateContext();
        var creditService = new CreditService(context);
        var purchaseService = new CreditPurchaseService(context, creditService);

        var packages = await purchaseService.GetPackagesAsync();

        Assert.Equal(4, packages.Count);
        Assert.Contains(packages, p => p.Name == "Study Lite" && p.BaseCredits == 300 && p.TotalCredits == 300 && p.Price == 10000m);
        Assert.Contains(packages, p => p.Name == "Final Sprint" && p.BonusCredits == 1000 && p.TotalCredits == 5000 && p.Price == 129000m);
    }

    [Fact]
    public async Task CreateManualTopUpAsync_CreatesCompletedPurchaseAndLedger()
    {
        await using var context = CreateContext();
        var userId = SeedUser(context, "Student");
        var adminId = SeedUser(context, "Admin");
        var creditService = new CreditService(context);
        var purchaseService = new CreditPurchaseService(context, creditService);

        var purchase = await purchaseService.CreateManualTopUpAsync(userId, 300, 10000m, "VND", adminId, "manual top-up");

        Assert.Equal(CreditPurchaseStatus.COMPLETED, purchase.Status);
        Assert.Equal(300, purchase.TotalCredits);
        Assert.Contains(context.CreditLedgers, l => l.UserId == userId && l.PaidCreditsAdded == 300 && l.Type == CreditLedgerType.ADMIN_GRANT);
        var wallet = await context.CreditWallets.SingleAsync(w => w.UserId == userId);
        Assert.Equal(300, wallet.PaidCredits);
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

    private static Guid SeedUser(AppDbContext context, string role)
    {
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            UserId = userId,
            FullName = role,
            Email = $"{userId:N}@example.edu",
            Username = $"{role.ToLowerInvariant()}-{userId:N}",
            PasswordHash = "hash",
            Role = role,
            IsApproved = true
        });
        context.SaveChanges();
        return userId;
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
