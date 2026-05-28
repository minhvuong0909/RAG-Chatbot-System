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
                entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            });

            // Dataset configuration
            modelBuilder.Entity<Dataset>(entity =>
            {
                entity.HasKey(e => e.DatasetId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                
                entity.HasOne(d => d.Creator)
                    .WithMany(u => u.Datasets)
                    .HasForeignKey(d => d.CreatedBy)
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
