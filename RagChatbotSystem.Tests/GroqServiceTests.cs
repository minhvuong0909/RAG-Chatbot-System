using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RagChatbotSystem.Business.Services;

namespace RagChatbotSystem.Tests;

public sealed class GroqServiceTests
{
    [Fact]
    public async Task GenerateAnswerStreamAsync_AcceptsFinalUsageFrameWithEmptyChoices()
    {
        const string sse = """
            data: {"choices":[{"delta":{"content":"Xin chào"}}]}

            data: {"choices":[],"usage":{"prompt_tokens":10,"completion_tokens":3,"total_tokens":13}}

            data: [DONE]

            """;
        var handler = new SseHandler(sse);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.groq.com/openai/v1/")
        };
        var configuration = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configuration.Setup(c => c["Groq:ApiKey"]).Returns("test-key");
        configuration.Setup(c => c["Groq:Model"]).Returns("test-model");
        var service = new GroqService(client, configuration.Object, NullLogger<GroqService>.Instance);

        var chunks = new List<string>();
        await foreach (var chunk in service.GenerateAnswerStreamAsync("prompt"))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["Xin chào"], chunks);
        Assert.Equal(10, service.LastPromptTokens);
        Assert.Equal(3, service.LastCompletionTokens);
        Assert.Equal(13, service.LastTotalTokens);
        Assert.True(service.LastWasActualTokenUsage);
        Assert.Equal("text/event-stream", handler.AcceptMediaType);
        Assert.Contains("\"stream\":true", handler.RequestBody);
    }

    private sealed class SseHandler(string content) : HttpMessageHandler
    {
        public string? AcceptMediaType { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AcceptMediaType = request.Headers.Accept.FirstOrDefault()?.MediaType;
            RequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/event-stream")
            };
        }
    }
}
