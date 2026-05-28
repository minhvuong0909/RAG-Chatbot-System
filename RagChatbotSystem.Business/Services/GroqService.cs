using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Business.Services
{
    public class GroqService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly bool _hasApiKey;

        public GroqService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _model = configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
            // Kiểm tra API key đã được cấu hình ở DI level 
            _hasApiKey = !string.IsNullOrWhiteSpace(configuration["Groq:ApiKey"]);
        }

        public async Task<string> GenerateAnswerAsync(string prompt)
        {
            if (!_hasApiKey)
            {
                return ExtractFallbackAnswer(prompt, "[MOCK ANSWER - Hãy cấu hình Groq:ApiKey trong appsettings.json]");
            }

            try
            {
                // BaseAddress + Authorization header đã được cấu hình sẵn từ Program.cs
                var payload = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.5
                };

                // Gọi trực tiếp endpoint tương đối (BaseAddress đã có sẵn)
                var response = await _httpClient.PostAsJsonAsync("chat/completions", payload);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<GroqResponse>();
                return result?.Choices?[0]?.Message?.Content ?? "Không nhận được phản hồi từ Groq.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Groq API: {ex.Message}");
                
                return ExtractFallbackAnswer(prompt, $"[LƯU Ý: Lỗi kết nối Groq API ({ex.Message}). Câu trả lời được trích xuất trực tiếp từ tài liệu của bạn]");
            }
        }

        /// <summary>
        /// Trích xuất ngữ cảnh từ prompt khi API không khả dụng.
        /// Dùng ReadOnlySpan để tối ưu thao tác cắt chuỗi.
        /// </summary>
        private static string ExtractFallbackAnswer(string prompt, string prefix)
        {
            const string contextMarker = "Ngữ cảnh:\n";
            const string questionMarker = "\n\nCâu hỏi:";

            var contextStart = prompt.IndexOf(contextMarker, StringComparison.Ordinal);
            if (contextStart < 0) return prefix;

            contextStart += contextMarker.Length;
            var contextEnd = prompt.IndexOf(questionMarker, contextStart, StringComparison.Ordinal);
            if (contextEnd <= contextStart) return prefix;

            var context = prompt.AsSpan(contextStart, contextEnd - contextStart).Trim();
            return $"{prefix}:\n\n{context.ToString()}";
        }

        // Các class để deserialize phản hồi từ Groq API
        private class GroqResponse
        {
            [JsonPropertyName("choices")]
            public Choice[]? Choices { get; set; }
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
