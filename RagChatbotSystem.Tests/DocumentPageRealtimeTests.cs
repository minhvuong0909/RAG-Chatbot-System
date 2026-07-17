using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Presentation.Pages.Documents;
using RagChatbotSystem.Presentation.Realtime;

namespace RagChatbotSystem.Tests;

public sealed class DocumentPageRealtimeTests
{
    [Fact]
    public async Task Upload_BroadcastsRefreshedDatasetDocumentCount()
    {
        var fixture = CreateFixture(documentCount: 1);
        var fileBytes = "sample document"u8.ToArray();
        var file = new FormFile(new MemoryStream(fileBytes), 0, fileBytes.Length, "file", "sample.txt");

        fixture.Documents
            .Setup(service => service.UploadDocumentAsync(
                fixture.DatasetId,
                fixture.UserId,
                It.IsAny<Stream>(),
                "sample.txt",
                fileBytes.Length,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixture.Document);
        fixture.Documents
            .Setup(service => service.ProcessUploadedDocumentAsync(fixture.Document.DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixture.Document with { Status = "Completed" });

        await fixture.Page.OnPostUploadAsync(fixture.DatasetId, file);

        fixture.Realtime.Verify(notifier => notifier.DatasetChangedAsync(
            "documents-changed",
            It.Is<DatasetDto>(dataset => dataset.DatasetId == fixture.DatasetId && dataset.DocumentCount == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_BroadcastsRefreshedDatasetDocumentCount()
    {
        var fixture = CreateFixture(documentCount: 0);
        fixture.Documents
            .Setup(service => service.GetDocumentAsync(fixture.Document.DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixture.Document);
        fixture.Documents
            .Setup(service => service.DeleteDocumentAsync(fixture.Document.DocumentId, fixture.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await fixture.Page.OnPostDeleteDocumentAsync(fixture.DatasetId, fixture.Document.DocumentId);

        fixture.Realtime.Verify(notifier => notifier.DatasetChangedAsync(
            "documents-changed",
            It.Is<DatasetDto>(dataset => dataset.DatasetId == fixture.DatasetId && dataset.DocumentCount == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Fixture CreateFixture(int documentCount)
    {
        var userId = Guid.NewGuid();
        var datasetId = Guid.NewGuid();
        var document = new DocumentDto(
            Guid.NewGuid(), datasetId, "sample.txt", "sample.txt", "txt", 15, "Uploaded",
            userId, DateTime.UtcNow, DateTime.UtcNow);
        var dataset = new DatasetDto(
            datasetId, "PRN222", null, userId, DateTime.UtcNow, DateTime.UtcNow,
            documentCount, true, true, userId, "Teacher");

        var datasets = new Mock<IDatasetService>();
        datasets.Setup(service => service.GetDatasetAsync(datasetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dataset);
        datasets.Setup(service => service.CanManageDatasetAsync(userId, "Teacher", datasetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var documents = new Mock<IDocumentService>();
        var realtime = new Mock<IRealtimeNotifier>();
        realtime.Setup(notifier => notifier.DocumentProgressAsync(
                It.IsAny<Guid>(), It.IsAny<DocumentDto>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        realtime.Setup(notifier => notifier.DatasetChangedAsync(
                It.IsAny<string>(), It.IsAny<DatasetDto?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, "Teacher")
            }, "Test"))
        };
        var page = new IndexModel(datasets.Object, documents.Object, realtime.Object, NullLogger<IndexModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = httpContext }
        };

        return new Fixture(userId, datasetId, document, page, documents, realtime);
    }

    private sealed record Fixture(
        Guid UserId,
        Guid DatasetId,
        DocumentDto Document,
        IndexModel Page,
        Mock<IDocumentService> Documents,
        Mock<IRealtimeNotifier> Realtime);
}
