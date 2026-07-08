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
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);

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
                entity.HasData(new SystemSetting
                {
                    Id = 1,
                    ChunkSize = 500,
                    ChunkOverlap = 100,
                    DailyTokenLimit = 50000,
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
                
                entity.HasIndex(e => new { e.UserId, e.Date }).IsUnique();

                entity.HasOne(utu => utu.User)
                    .WithMany()
                    .HasForeignKey(utu => utu.UserId)
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
        }
    }
}
