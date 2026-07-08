using System;
using System.Collections.Generic;
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

        private int _lastPromptTokens;
        private int _lastCompletionTokens;
        private int _lastTotalTokens;

        public int LastPromptTokens => _lastPromptTokens;
        public int LastCompletionTokens => _lastCompletionTokens;
        public int LastTotalTokens => _lastTotalTokens;

        public OpenAiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["OpenAi:ApiKey"] ?? string.Empty;
            _model = configuration["OpenAi:Model"] ?? "gpt-4o-mini";
        }

        public async Task<string> GenerateAnswerAsync(string prompt)
        {
            _lastPromptTokens = 0;
            _lastCompletionTokens = 0;
            _lastTotalTokens = 0;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                string mockAnswer = "[MOCK ANSWER - Please configure OpenAi:ApiKey in appsettings.json]";
                if (prompt.Contains("Ngữ cảnh:") && prompt.Contains("Câu hỏi:"))
                {
                    var contextStart = prompt.IndexOf("Ngữ cảnh:\n") + 10;
                    var contextEnd = prompt.IndexOf("\n\nCâu hỏi:");
                    if (contextEnd > contextStart)
                    {
                        var context = prompt.Substring(contextStart, contextEnd - contextStart);
                        mockAnswer = $"[MOCK ANSWER - Please configure OpenAi:ApiKey in appsettings.json]:\n\n{context.Trim()}";
                    }
                }
                
                _lastPromptTokens = prompt.Length / 4;
                _lastCompletionTokens = mockAnswer.Length / 4;
                _lastTotalTokens = _lastPromptTokens + _lastCompletionTokens;
                return mockAnswer;
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
                var content = result?.Choices?[0]?.Message?.Content ?? "Không nhận được phản hồi từ OpenAI.";

                _lastPromptTokens = prompt.Length / 4;
                _lastCompletionTokens = content.Length / 4;
                _lastTotalTokens = _lastPromptTokens + _lastCompletionTokens;

                return content;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling OpenAI API: {ex.Message}");
                
                string errorAnswer = $"Lỗi khi kết nối với OpenAI: {ex.Message}";
                if (prompt.Contains("Ngữ cảnh:") && prompt.Contains("Câu hỏi:"))
                {
                    var contextStart = prompt.IndexOf("Ngữ cảnh:\n") + 10;
                    var contextEnd = prompt.IndexOf("\n\nCâu hỏi:");
                    if (contextEnd > contextStart)
                    {
                        var context = prompt.Substring(contextStart, contextEnd - contextStart);
                        errorAnswer = $"[LƯU Ý: Lỗi kết nối OpenAI API ({ex.Message}). Câu trả lời được trích xuất trực tiếp từ tài liệu của bạn]:\n\n{context.Trim()}";
                    }
                }

                _lastPromptTokens = prompt.Length / 4;
                _lastCompletionTokens = errorAnswer.Length / 4;
                _lastTotalTokens = _lastPromptTokens + _lastCompletionTokens;
                return errorAnswer;
            }
        }

        public async IAsyncEnumerable<string> GenerateAnswerStreamAsync(string prompt)
        {
            _lastPromptTokens = 0;
            _lastCompletionTokens = 0;
            _lastTotalTokens = 0;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                var mockText = $"[MOCK ANSWER - Please configure OpenAi:ApiKey in appsettings.json]";
                
                _lastPromptTokens = prompt.Length / 4;
                _lastCompletionTokens = mockText.Length / 4;
                _lastTotalTokens = _lastPromptTokens + _lastCompletionTokens;

                var words = mockText.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    yield return word + " ";
                    await Task.Delay(30);
                }
                yield break;
            }

            HttpResponseMessage? response = null;
            var accumulatedText = new System.Text.StringBuilder();
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
                    temperature = 0.7,
                    stream = true
                };

                requestMessage.Content = JsonContent.Create(payload);
                response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
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

                        OpenAiStreamResponse? chunk = null;
                        try
                        {
                            chunk = System.Text.Json.JsonSerializer.Deserialize<OpenAiStreamResponse>(data);
                        }
                        catch
                        {
                            // Ignore malformed json in stream
                        }

                        var text = chunk?.Choices?[0]?.Delta?.Content;
                        if (!string.IsNullOrEmpty(text))
                        {
                            accumulatedText.Append(text);
                            yield return text;
                        }
                    }
                }
            }
            finally
            {
                response?.Dispose();

                _lastPromptTokens = prompt.Length / 4;
                _lastCompletionTokens = accumulatedText.Length / 4;
                _lastTotalTokens = _lastPromptTokens + _lastCompletionTokens;
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

        // Classes for streaming
        private class OpenAiStreamResponse
        {
            [JsonPropertyName("choices")]
            public ChoiceStream[] Choices { get; set; } = Array.Empty<ChoiceStream>();
        }

        private class ChoiceStream
        {
            [JsonPropertyName("delta")]
            public DeltaMessage Delta { get; set; } = null!;
        }

        private class DeltaMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }
    }
}
