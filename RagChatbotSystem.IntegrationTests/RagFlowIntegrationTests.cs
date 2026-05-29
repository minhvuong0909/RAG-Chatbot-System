using Microsoft.EntityFrameworkCore;
using Pgvector;
using RagChatbotSystem.Business.Constants;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;
using Xunit;

namespace RagChatbotSystem.IntegrationTests;

public class RagFlowIntegrationTests
{
    [Fact]
    public async Task GoogleLoginWithConfiguredAdminEmail_AssignsAdminRole()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        var email = $"admin-{Guid.NewGuid():N}@example.com";
        var accountService = new AccountService(context);

        var user = await accountService.FindOrCreateGoogleUserAsync(
            email,
            "Configured Admin",
            new[] { email });

        try
        {
            Assert.Equal(UserRoles.Admin, user.Role);

            var userFromDatabase = await context.Users.SingleAsync(dbUser => dbUser.UserId == user.UserId);
            Assert.Equal(UserRoles.Admin, userFromDatabase.Role);
        }
        finally
        {
            context.Users.Remove(user);
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task UploadIndexChatAndCitationFlow_CompletesSuccessfully()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        var runId = Guid.NewGuid();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Integration Test User",
            Email = $"integration-{runId:N}@example.com",
            Role = UserRoles.User,
            CreatedAt = DateTime.UtcNow
        };

        var dataset = new Dataset
        {
            DatasetId = Guid.NewGuid(),
            Name = $"Integration Dataset {runId:N}",
            Description = "Created by integration test",
            CreatedBy = user.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        context.Datasets.Add(dataset);
        await context.SaveChangesAsync();

        try
        {
            var fakeRagApi = new FakeRagApiClient();
            var documentService = new DocumentService(context, fakeRagApi);

            var rawText = string.Join("\n\n", Enumerable.Repeat(
                "The Burj Khalifa is the tallest building in the world and is located in Dubai, United Arab Emirates.",
                12));

            var document = await documentService.ProcessAndIndexDocumentAsync(
                dataset.DatasetId,
                user.UserId,
                "integration_facts.txt",
                rawText);

            Assert.NotNull(document);
            Assert.Equal("Completed", document.Status);

            var chunkCount = await context.Chunks.CountAsync(chunk => chunk.DocumentId == document.DocumentId);
            var vectorCount = await context.VectorRecords.CountAsync(vector => vector.DocumentId == document.DocumentId);

            Assert.True(chunkCount > 0);
            Assert.Equal(chunkCount, vectorCount);

            var session = new ChatSession
            {
                SessionId = Guid.NewGuid(),
                UserId = user.UserId,
                DatasetId = dataset.DatasetId,
                Title = "Integration Chat Session",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.ChatSessions.Add(session);
            await context.SaveChangesAsync();

            var chatService = new ChatService(context, fakeRagApi, new FakeLlmService());
            var assistantMessage = await chatService.SendChatMessageAsync(
                session.SessionId,
                "Where is the Burj Khalifa located?");

            Assert.Equal("Assistant", assistantMessage.Role);
            Assert.Contains("Dubai", assistantMessage.Content, StringComparison.OrdinalIgnoreCase);

            var messages = await context.ChatMessages
                .Where(message => message.SessionId == session.SessionId)
                .ToListAsync();

            var citations = await context.Citations
                .Where(citation => citation.MessageId == assistantMessage.MessageId)
                .ToListAsync();

            Assert.Equal(2, messages.Count);
            Assert.NotEmpty(citations);
            Assert.All(citations, citation => Assert.Equal(document.DocumentId, citation.DocumentId));
        }
        finally
        {
            context.Datasets.Remove(dataset);
            await context.SaveChangesAsync();

            context.Users.Remove(user);
            await context.SaveChangesAsync();
        }
    }

    private static AppDbContext CreateDbContext()
    {
        var connectionString = Environment.GetEnvironmentVariable("RAG_TEST_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5434;Database=rag_chatbot;Username=rag_user;Password=123456";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeRagApiClient : IRagApiClient
    {
        private readonly List<DocumentModelDto> _documents = new();

        public Task<IndexResponseDto> IndexDocumentsAsync(IndexRequestDto request)
        {
            _documents.Clear();
            _documents.AddRange(request.Documents);

            return Task.FromResult(new IndexResponseDto
            {
                Message = "Indexed by fake RAG API",
                Embeddings = request.Documents
                    .Select(_ => Enumerable.Repeat(0.01f, 384).ToArray())
                    .ToList()
            });
        }

        public Task<RetrieveResponseDto> RetrieveAsync(RetrieveRequestDto request)
        {
            return Task.FromResult(new RetrieveResponseDto
            {
                Query = request.Query,
                Documents = _documents.ToList(),
                Scores = _documents.Select(_ => 1.0).ToList(),
                Trace = new List<string> { "fake-retrieve" }
            });
        }

        public Task<bool> DeleteDocumentAsync(Guid documentId)
        {
            _documents.RemoveAll(document =>
                document.Metadata.TryGetValue("document_id", out var value)
                && string.Equals(value?.ToString(), documentId.ToString(), StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(true);
        }
    }

    private sealed class FakeLlmService : ILlmService
    {
        public Task<string> GenerateAnswerAsync(string prompt)
        {
            return Task.FromResult("The Burj Khalifa is located in Dubai, United Arab Emirates.");
        }
    }
}
