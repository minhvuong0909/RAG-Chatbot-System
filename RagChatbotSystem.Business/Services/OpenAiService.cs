using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Business.Services
{
    public class OpenAiService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public OpenAiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["OpenAi:ApiKey"] ?? string.Empty;
            _model = configuration["OpenAi:Model"] ?? "gpt-4o-mini";
        }

        public async Task<string> GenerateAnswerAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                // Fallback mock response if API Key is not set
                if (prompt.Contains("Ngữ cảnh:") && prompt.Contains("Câu hỏi:"))
                {
                    var contextStart = prompt.IndexOf("Ngữ cảnh:\n") + 10;
                    var contextEnd = prompt.IndexOf("\n\nCâu hỏi:");
                    if (contextEnd > contextStart)
                    {
                        var context = prompt.Substring(contextStart, contextEnd - contextStart);
                        return $"[MOCK ANSWER - Please configure OpenAi:ApiKey in appsettings.json]:\n\n{context.Trim()}";
                    }
                }
                return "[MOCK ANSWER - Please configure OpenAi:ApiKey in appsettings.json]";
            }

            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var payload = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.7
                };

                requestMessage.Content = JsonContent.Create(payload);
                var response = await _httpClient.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OpenAiResponse>();
                return result?.Choices?[0]?.Message?.Content ?? "Không nhận được phản hồi từ OpenAI.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling OpenAI API: {ex.Message}");
                
                // Fallback context extraction on actual API failure
                if (prompt.Contains("Ngữ cảnh:") && prompt.Contains("Câu hỏi:"))
                {
                    var contextStart = prompt.IndexOf("Ngữ cảnh:\n") + 10;
                    var contextEnd = prompt.IndexOf("\n\nCâu hỏi:");
                    if (contextEnd > contextStart)
                    {
                        var context = prompt.Substring(contextStart, contextEnd - contextStart);
                        return $"[LƯU Ý: Lỗi kết nối OpenAI API ({ex.Message}). Câu trả lời được trích xuất trực tiếp từ tài liệu của bạn]:\n\n{context.Trim()}";
                    }
                }
                return $"Lỗi khi kết nối với OpenAI: {ex.Message}";
            }
        }

        private class OpenAiResponse
        {
            [JsonPropertyName("choices")]
            public Choice[] Choices { get; set; } = Array.Empty<Choice>();
        }

        private class Choice
        {
            [JsonPropertyName("message")]
            public Message Message { get; set; } = null!;
        }

        private class Message
        {
            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }
    }
}
