using Microsoft.AspNetCore.SignalR;
using Moq;
using RagChatbotSystem.Presentation.Hubs;
using RagChatbotSystem.Presentation.Services;

namespace RagChatbotSystem.Tests;

public sealed class RealtimeServiceTests
{
    [Fact]
    public async Task SendDocumentProgressAsync_UsesNotificationHubForDatasetAndAdmin()
    {
        var datasetId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var clients = new Mock<IHubClients>();
        var datasetClient = new Mock<IClientProxy>();
        var adminClient = new Mock<IClientProxy>();
        clients.Setup(client => client.Group(NotificationHub.DatasetGroupName(datasetId)))
            .Returns(datasetClient.Object);
        clients.Setup(client => client.Group(NotificationHub.AdminGroupName))
            .Returns(adminClient.Object);

        datasetClient.Setup(client => client.SendCoreAsync(
                "DocumentProgress",
                It.Is<object?[]>(arguments => HasProgressPayload(arguments, datasetId, documentId)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        adminClient.Setup(client => client.SendCoreAsync(
                "DocumentProgress",
                It.Is<object?[]>(arguments => HasProgressPayload(arguments, datasetId, documentId)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var chatHub = new Mock<IHubContext<ChatHub>>();
        var notificationHub = new Mock<IHubContext<NotificationHub>>();
        notificationHub.SetupGet(hub => hub.Clients).Returns(clients.Object);
        var service = new RealtimeService(chatHub.Object, notificationHub.Object);

        await service.SendDocumentProgressAsync(datasetId, documentId, "Processing", 50);

        datasetClient.VerifyAll();
        adminClient.VerifyAll();
    }

    private static bool HasProgressPayload(object?[] arguments, Guid datasetId, Guid documentId)
    {
        if (arguments.Length != 1 || arguments[0] == null)
        {
            return false;
        }

        var payload = arguments[0]!;
        var type = payload.GetType();
        return Equals(type.GetProperty("datasetId")?.GetValue(payload), datasetId)
            && Equals(type.GetProperty("documentId")?.GetValue(payload), documentId)
            && Equals(type.GetProperty("status")?.GetValue(payload), "Processing")
            && Equals(type.GetProperty("percentComplete")?.GetValue(payload), 50);
    }
}
