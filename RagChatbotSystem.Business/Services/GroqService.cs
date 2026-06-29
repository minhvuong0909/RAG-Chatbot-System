using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Business.Services
{
    public class GroqService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GroqService> _logger;
        private readonly string _model;
        private readonly bool _hasApiKey;

        public GroqService(HttpClient httpClient, IConfiguration configuration, ILogger<GroqService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
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
                _logger.LogError(ex, "Groq API call failed.");
                
                return ExtractFallbackAnswer(prompt, $"[LƯU Ý: Lỗi kết nối Groq API ({ex.Message}). Câu trả lời được trích xuất trực tiếp từ tài liệu của bạn]");
            }
        }

        public async IAsyncEnumerable<string> GenerateAnswerStreamAsync(string prompt)
        {
            if (!_hasApiKey)
            {
                var mockText = ExtractFallbackAnswer(prompt, "[MOCK ANSWER - Hãy cấu hình Groq:ApiKey trong appsettings.json]");
                var words = mockText.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    yield return word + " ";
                    await Task.Delay(40);
                }
                yield break;
            }

            HttpResponseMessage? response = null;
            try
            {
                var payload = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.5,
                    stream = true
                };

                // Groq API client config has BaseAddress
                response = await _httpClient.PostAsJsonAsync("chat/completions", payload);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("data: "))
                    {
                        var data = trimmed.Substring(6).Trim();
                        if (data == "[DONE]")
                        {
                            break;
                        }

                        GroqStreamResponse? chunk = null;
                        try
                        {
                            chunk = System.Text.Json.JsonSerializer.Deserialize<GroqStreamResponse>(data);
                        }
                        catch
                        {
                            // Ignore json deserialization issues on stream progress
                        }

                        var text = chunk?.Choices?[0]?.Delta?.Content;
                        if (!string.IsNullOrEmpty(text))
                        {
                            yield return text;
                        }
                    }
                }
            }
            finally
            {
                response?.Dispose();
            }
        }

        /// <summary>
        /// Trích xuất ngữ cảnh từ prompt và định dạng gọn gàng khi API không khả dụng.
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

            var context = prompt.Substring(contextStart, contextEnd - contextStart).Trim();
            
            // Tách các đoạn ngữ cảnh bằng các dấu phân tách phổ biến
            var sections = context.Split(new[] { "---", "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            var cleanSections = new List<string>();
            
            foreach (var section in sections)
            {
                var cleanSec = section.Trim();
                if (string.IsNullOrWhiteSpace(cleanSec)) continue;
                
                // Giới hạn độ dài mỗi đoạn cho gọn gàng
                if (cleanSec.Length > 200)
                {
                    cleanSec = cleanSec.Substring(0, 200) + "...";
                }
                cleanSections.Add(cleanSec);
            }

            var question = "";
            var questionStart = prompt.IndexOf(questionMarker, StringComparison.Ordinal);
            if (questionStart >= 0)
            {
                var qText = prompt.Substring(questionStart + questionMarker.Length).Trim();
                var lines = qText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    question = lines[0].Trim();
                }
            }

            var responseBuilder = new System.Text.StringBuilder();
            responseBuilder.AppendLine(prefix);
            if (!string.IsNullOrEmpty(question))
            {
                responseBuilder.AppendLine($"\n**Câu hỏi:** *{question}*");
            }
            responseBuilder.AppendLine("\n**Đoạn trích tìm thấy trong tài liệu:**");
            
            for (int i = 0; i < cleanSections.Count; i++)
            {
                responseBuilder.AppendLine($"\n{i + 1}. \"{cleanSections[i]}\"");
            }

            return responseBuilder.ToString();
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

        // Classes for streaming
        private class GroqStreamResponse
        {
            [JsonPropertyName("choices")]
            public ChoiceStream[]? Choices { get; set; }
        }

        private class ChoiceStream
        {
            [JsonPropertyName("delta")]
            public DeltaMessage? Delta { get; set; }
        }

        private class DeltaMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }
    }
}
