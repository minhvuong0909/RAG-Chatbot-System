using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using RagChatbotSystem.Presentation.Hubs;

namespace RagChatbotSystem.Tests;

public sealed class HubAuthorizationTests
{
    [Theory]
    [InlineData(typeof(ChatHub))]
    [InlineData(typeof(DocumentHub))]
    [InlineData(typeof(NotificationHub))]
    public void RealtimeHubs_RequireAuthenticatedUsers(Type hubType)
    {
        Assert.NotNull(hubType.GetCustomAttribute<AuthorizeAttribute>());
    }
}
