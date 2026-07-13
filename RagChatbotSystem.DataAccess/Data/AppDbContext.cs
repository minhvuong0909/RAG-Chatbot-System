using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.DataAccess.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Dataset> Datasets { get; set; } = null!;
        public DbSet<Document> Documents { get; set; } = null!;
        public DbSet<Chunk> Chunks { get; set; } = null!;
        public DbSet<VectorRecord> VectorRecords { get; set; } = null!;
        public DbSet<ChatSession> ChatSessions { get; set; } = null!;
        public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
        public DbSet<Citation> Citations { get; set; } = null!;
        public DbSet<DatasetPermission> DatasetPermissions { get; set; } = null!;
        public DbSet<TeacherSubjectAssignment> TeacherSubjectAssignments { get; set; } = null!;
        public DbSet<SystemSetting> SystemSettings { get; set; } = null!;
        public DbSet<UserTokenUsage> UserTokenUsages { get; set; } = null!;
        public DbSet<CreditWallet> CreditWallets { get; set; } = null!;
        public DbSet<CreditLedger> CreditLedgers { get; set; } = null!;
        public DbSet<CreditPackage> CreditPackages { get; set; } = null!;
        public DbSet<CreditPurchase> CreditPurchases { get; set; } = null!;
        public DbSet<CreditBlockedAttempt> CreditBlockedAttempts { get; set; } = null!;
        public DbSet<ModelComparisonRun> ModelComparisonRuns { get; set; } = null!;
        public DbSet<ModelComparisonResult> ModelComparisonResults { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Enable pgvector extension
            modelBuilder.HasPostgresExtension("vector");

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
                entity.Property(e => e.IsApproved).IsRequired().HasDefaultValue(true);
                entity.Property(e => e.MustChangePassword).IsRequired().HasDefaultValue(false);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();
            });

            // Dataset configuration
            modelBuilder.Entity<Dataset>(entity =>
            {
                entity.HasKey(e => e.DatasetId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.IsPublic).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.IsApproved).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.IsArchived).IsRequired().HasDefaultValue(false);
                entity.HasIndex(e => e.IsArchived);
                
                entity.HasOne(d => d.Creator)
                    .WithMany(u => u.Datasets)
                    .HasForeignKey(d => d.CreatedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // DatasetPermission configuration
            modelBuilder.Entity<DatasetPermission>(entity =>
            {
                entity.HasKey(e => e.PermissionId);
                entity.HasIndex(e => new { e.DatasetId, e.UserId }).IsUnique();

                entity.HasOne(dp => dp.Dataset)
                    .WithMany(d => d.DatasetPermissions)
                    .HasForeignKey(dp => dp.DatasetId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(dp => dp.User)
                    .WithMany(u => u.DatasetPermissions)
                    .HasForeignKey(dp => dp.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // TeacherSubjectAssignment configuration
            modelBuilder.Entity<TeacherSubjectAssignment>(entity =>
            {
                entity.HasKey(e => e.AssignmentId);
                entity.HasIndex(e => e.DatasetId).IsUnique();
                entity.HasIndex(e => new { e.TeacherId, e.DatasetId }).IsUnique();

                entity.HasOne(a => a.Dataset)
                    .WithOne(d => d.TeacherSubjectAssignment)
                    .HasForeignKey<TeacherSubjectAssignment>(a => a.DatasetId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Teacher)
                    .WithMany(u => u.TeacherSubjectAssignments)
                    .HasForeignKey(a => a.TeacherId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.AssignedByAdmin)
                    .WithMany()
                    .HasForeignKey(a => a.AssignedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            // Document configuration
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.DocumentId);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FileType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.FileHash).HasMaxLength(64);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ProcessError).HasMaxLength(2000);
                entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
                entity.HasIndex(e => new { e.DatasetId, e.FileHash });

                entity.HasOne(d => d.Dataset)
                    .WithMany(ds => ds.Documents)
                    .HasForeignKey(d => d.DatasetId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Uploader)
                    .WithMany(u => u.Documents)
                    .HasForeignKey(d => d.UploadedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Chunk configuration
            modelBuilder.Entity<Chunk>(entity =>
            {
                entity.HasKey(e => e.ChunkId);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.MetadataJson).HasColumnType("jsonb");

                entity.HasOne(c => c.Dataset)
                    .WithMany(ds => ds.Chunks)
                    .HasForeignKey(c => c.DatasetId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Document)
                    .WithMany(d => d.Chunks)
                    .HasForeignKey(c => c.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // VectorRecord configuration
            modelBuilder.Entity<VectorRecord>(entity =>
            {
                entity.HasKey(e => e.VectorId);
                entity.Property(e => e.EmbeddingModel).IsRequired().HasMaxLength(255);
                
                // Configure Vector type and dimensions
                entity.Property(e => e.Embedding)
                    .HasColumnType("vector(384)"); // Matches MiniLM-L6-v2 dimension

                entity.HasOne(vr => vr.Dataset)
                    .WithMany(ds => ds.VectorRecords)
                    .HasForeignKey(vr => vr.DatasetId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(vr => vr.Document)
                    .WithMany(d => d.VectorRecords)
                    .HasForeignKey(vr => vr.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(vr => vr.Chunk)
                    .WithOne(c => c.VectorRecord)
                    .HasForeignKey<VectorRecord>(vr => vr.ChunkId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ChatSession configuration
            modelBuilder.Entity<ChatSession>(entity =>
            {
                entity.HasKey(e => e.SessionId);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(255);

                entity.HasOne(cs => cs.User)
                    .WithMany(u => u.ChatSessions)
                    .HasForeignKey(cs => cs.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cs => cs.Dataset)
                    .WithMany(ds => ds.ChatSessions)
                    .HasForeignKey(cs => cs.DatasetId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ChatMessage configuration
            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(e => e.MessageId);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Content).IsRequired();

                entity.HasOne(cm => cm.ChatSession)
                    .WithMany(cs => cs.ChatMessages)
                    .HasForeignKey(cm => cm.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // SystemSetting configuration
            modelBuilder.Entity<SystemSetting>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DailyFreeCredits).IsRequired().HasDefaultValue(60);
                entity.Property(e => e.CreditTokenUnit).IsRequired().HasDefaultValue(1000);
                entity.Property(e => e.CreditOutputTokenWeight).IsRequired().HasDefaultValue(4);
                entity.Property(e => e.EnableCreditSystem).IsRequired().HasDefaultValue(true);
                entity.Property(e => e.ExamSeasonDailyFreeCredits).IsRequired().HasDefaultValue(100);
                entity.HasData(new SystemSetting
                {
                    Id = 1,
                    ChunkSize = 500,
                    ChunkOverlap = 100,
                    DailyTokenLimit = 50000,
                    DailyFreeCredits = 60,
                    CreditTokenUnit = 1000,
                    CreditOutputTokenWeight = 4,
                    EnableCreditSystem = true,
                    ExamSeasonDailyFreeCredits = 100,
                    UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });
            });

            // UserTokenUsage configuration
            modelBuilder.Entity<UserTokenUsage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Date).IsRequired();
                entity.Property(e => e.TokenCount).IsRequired().HasDefaultValue(0);
                entity.Property(e => e.QueryCount).IsRequired().HasDefaultValue(0);

                entity.HasIndex(e => new { e.UserId, e.DatasetId, e.Date }).IsUnique();

                entity.HasOne(utu => utu.User)
                    .WithMany()
                    .HasForeignKey(utu => utu.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(utu => utu.Dataset)
                    .WithMany()
                    .HasForeignKey(utu => utu.DatasetId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Citation configuration
            modelBuilder.Entity<Citation>(entity =>
            {
                entity.HasKey(e => e.CitationId);
                entity.Property(e => e.QuoteText).IsRequired();
                entity.Property(e => e.SourceLabel).HasMaxLength(255);

                entity.HasOne(c => c.ChatMessage)
                    .WithMany(cm => cm.Citations)
                    .HasForeignKey(c => c.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Chunk)
                    .WithMany(ch => ch.Citations)
                    .HasForeignKey(c => c.ChunkId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Document)
                    .WithMany(d => d.Citations)
                    .HasForeignKey(c => c.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CreditWallet>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FreeCredits).IsRequired().HasDefaultValue(0);
                entity.Property(e => e.PaidCredits).IsRequired().HasDefaultValue(0);
                entity.Property(e => e.LastFreeCreditResetDate).IsRequired();
                entity.Property(e => e.Version).IsConcurrencyToken();
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_CreditWallet_FreeCredits_NonNegative", "\"FreeCredits\" >= 0");
                    t.HasCheckConstraint("CK_CreditWallet_PaidCredits_NonNegative", "\"PaidCredits\" >= 0");
                });

                entity.HasOne(w => w.User)
                    .WithMany()
                    .HasForeignKey(w => w.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CreditPackage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Price).HasColumnType("numeric(18,2)");
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_CreditPackage_BaseCredits_Positive", "\"BaseCredits\" > 0");
                    t.HasCheckConstraint("CK_CreditPackage_BonusCredits_NonNegative", "\"BonusCredits\" >= 0");
                    t.HasCheckConstraint("CK_CreditPackage_TotalCredits_Positive", "\"TotalCredits\" > 0");
                    t.HasCheckConstraint("CK_CreditPackage_Price_NonNegative", "\"Price\" >= 0");
                });

                entity.HasData(
                    new CreditPackage
                    {
                        Id = new Guid("10000000-0000-0000-0000-000000000001"),
                        Name = "Study Lite",
                        Description = "Small top-up for regular study.",
                        BaseCredits = 300,
                        BonusCredits = 0,
                        TotalCredits = 300,
                        Price = 10000m,
                        Currency = "VND",
                        IsActive = true,
                        DisplayOrder = 1,
                        CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new CreditPackage
                    {
                        Id = new Guid("10000000-0000-0000-0000-000000000002"),
                        Name = "Study Plus",
                        Description = "Better value for active learners.",
                        BaseCredits = 700,
                        BonusCredits = 100,
                        TotalCredits = 800,
                        Price = 25000m,
                        Currency = "VND",
                        IsActive = true,
                        DisplayOrder = 2,
                        CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new CreditPackage
                    {
                        Id = new Guid("10000000-0000-0000-0000-000000000003"),
                        Name = "Exam Boost",
                        Description = "Recommended for quiz/PE/final preparation.",
                        BaseCredits = 1700,
                        BonusCredits = 300,
                        TotalCredits = 2000,
                        Price = 59000m,
                        Currency = "VND",
                        IsActive = true,
                        DisplayOrder = 3,
                        CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new CreditPackage
                    {
                        Id = new Guid("10000000-0000-0000-0000-000000000004"),
                        Name = "Final Sprint",
                        Description = "Best value for intensive exam preparation.",
                        BaseCredits = 4000,
                        BonusCredits = 1000,
                        TotalCredits = 5000,
                        Price = 129000m,
                        Currency = "VND",
                        IsActive = true,
                        DisplayOrder = 4,
                        CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    });
            });

            modelBuilder.Entity<CreditPurchase>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(10);
                entity.Property(e => e.PaymentProvider).HasMaxLength(80);
                entity.Property(e => e.ProviderReference).HasMaxLength(200);
                entity.Property(e => e.CheckoutUrl).HasMaxLength(1000);
                entity.Property(e => e.Amount).HasColumnType("numeric(18,2)");
                entity.HasIndex(e => new { e.UserId, e.CreatedAt });
                entity.HasIndex(e => new { e.Status, e.CreatedAt });
                entity.HasIndex(e => e.ProviderOrderCode).IsUnique();
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_CreditPurchase_BaseCredits_NonNegative", "\"BaseCredits\" >= 0");
                    t.HasCheckConstraint("CK_CreditPurchase_BonusCredits_NonNegative", "\"BonusCredits\" >= 0");
                    t.HasCheckConstraint("CK_CreditPurchase_TotalCredits_NonNegative", "\"TotalCredits\" >= 0");
                    t.HasCheckConstraint("CK_CreditPurchase_Amount_NonNegative", "\"Amount\" >= 0");
                });

                entity.HasOne(p => p.User)
                    .WithMany()
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Package)
                    .WithMany(pkg => pkg.Purchases)
                    .HasForeignKey(p => p.PackageId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(p => p.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(p => p.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<CreditLedger>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).HasConversion<string>().IsRequired().HasMaxLength(30);
                entity.Property(e => e.ModelName).HasMaxLength(120);
                entity.Property(e => e.Note).HasMaxLength(1000);
                entity.HasIndex(e => new { e.UserId, e.CreatedAt });
                entity.HasIndex(e => new { e.DatasetId, e.CreatedAt });
                entity.HasIndex(e => new { e.ModelName, e.CreatedAt });
                entity.HasIndex(e => new { e.Type, e.CreatedAt });
                entity.HasIndex(e => e.ChatMessageId);
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_CreditLedger_CalculatedCredits_NonNegative", "\"CalculatedCredits\" >= 0");
                    t.HasCheckConstraint("CK_CreditLedger_ChargedCredits_NonNegative", "\"ChargedCredits\" >= 0");
                    t.HasCheckConstraint("CK_CreditLedger_FreeCreditsUsed_NonNegative", "\"FreeCreditsUsed\" >= 0");
                    t.HasCheckConstraint("CK_CreditLedger_PaidCreditsUsed_NonNegative", "\"PaidCreditsUsed\" >= 0");
                    t.HasCheckConstraint("CK_CreditLedger_FreeCreditsAdded_NonNegative", "\"FreeCreditsAdded\" >= 0");
                    t.HasCheckConstraint("CK_CreditLedger_PaidCreditsAdded_NonNegative", "\"PaidCreditsAdded\" >= 0");
                    t.HasCheckConstraint("CK_CreditLedger_BalanceBeforeFree_NonNegative", "\"BalanceBeforeFree\" >= 0");
                    t.HasCheckConstraint("CK_CreditLedger_BalanceBeforePaid_NonNegative", "\"BalanceBeforePaid\" >= 0");
                    t.HasCheckConstraint("CK_CreditLedger_BalanceAfterFree_NonNegative", "\"BalanceAfterFree\" >= 0");
                    t.HasCheckConstraint("CK_CreditLedger_BalanceAfterPaid_NonNegative", "\"BalanceAfterPaid\" >= 0");
                    t.HasCheckConstraint("CK_CreditLedger_Tokens_NonNegative", "\"InputTokens\" >= 0 AND \"OutputTokens\" >= 0 AND \"TotalTokens\" >= 0");
                });

                entity.HasOne(l => l.User)
                    .WithMany()
                    .HasForeignKey(l => l.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.Dataset)
                    .WithMany()
                    .HasForeignKey(l => l.DatasetId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(l => l.ChatSession)
                    .WithMany()
                    .HasForeignKey(l => l.ChatSessionId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(l => l.ChatMessage)
                    .WithMany()
                    .HasForeignKey(l => l.ChatMessageId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(l => l.RelatedPackage)
                    .WithMany(p => p.LedgerEntries)
                    .HasForeignKey(l => l.RelatedPackageId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(l => l.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(l => l.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<CreditBlockedAttempt>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Reason).HasConversion<string>().IsRequired().HasMaxLength(40);
                entity.Property(e => e.MessagePreview).HasMaxLength(500);
                entity.Property(e => e.Note).HasMaxLength(1000);
                entity.HasIndex(e => new { e.UserId, e.CreatedAt });
                entity.HasIndex(e => new { e.Reason, e.CreatedAt });

                entity.HasOne(b => b.User)
                    .WithMany()
                    .HasForeignKey(b => b.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.Dataset)
                    .WithMany()
                    .HasForeignKey(b => b.DatasetId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(b => b.ChatSession)
                    .WithMany()
                    .HasForeignKey(b => b.ChatSessionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ModelComparisonRun>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Question).IsRequired().HasMaxLength(1000);
                entity.HasIndex(e => new { e.DatasetId, e.CreatedAt });
                entity.HasIndex(e => new { e.RunByUserId, e.CreatedAt });

                entity.HasOne(r => r.Dataset)
                    .WithMany()
                    .HasForeignKey(r => r.DatasetId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.RunByUser)
                    .WithMany()
                    .HasForeignKey(r => r.RunByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ModelComparisonResult>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProviderKey).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ModelName).IsRequired().HasMaxLength(120);
                entity.HasIndex(e => new { e.ProviderKey, e.ModelComparisonRunId });

                entity.HasOne(res => res.Run)
                    .WithMany(r => r.Results)
                    .HasForeignKey(res => res.ModelComparisonRunId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
