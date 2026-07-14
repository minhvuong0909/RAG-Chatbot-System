using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using RagChatbotSystem.Presentation.Pages.Admin.ModelComparison;

namespace RagChatbotSystem.Tests;

public class ModelComparisonAuthorizationTests
{
    [Theory]
    [InlineData(typeof(IndexModel))]
    [InlineData(typeof(HistoryModel))]
    public void ModelComparisonPages_RequireAdminRoleOnly(Type pageModelType)
    {
        var authorizeAttribute = pageModelType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal("Admin", authorizeAttribute.Roles);
    }
}
