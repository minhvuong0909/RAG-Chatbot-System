using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Business.Services
{
    public class GroqService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GroqService> _logger;
        private readonly string _model;
        private readonly bool _hasApiKey;

        private int _lastPromptTokens;
        private int _lastCompletionTokens;
        private int _lastTotalTokens;
        private bool _lastWasActualTokenUsage;
        private bool _lastIsProviderFallback;
        private string? _lastErrorMessage;

        public int LastPromptTokens => _lastPromptTokens;
        public int LastCompletionTokens => _lastCompletionTokens;
        public int LastTotalTokens => _lastTotalTokens;
        public bool LastWasActualTokenUsage => _lastWasActualTokenUsage;
        public bool LastIsProviderFallback => _lastIsProviderFallback;
        public string? LastErrorMessage => _lastErrorMessage;
        public string ModelName => _model;

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
            _lastPromptTokens = 0;
            _lastCompletionTokens = 0;
            _lastTotalTokens = 0;
            _lastWasActualTokenUsage = false;
            _lastIsProviderFallback = false;
            _lastErrorMessage = null;

            if (!_hasApiKey)
            {
                _lastIsProviderFallback = true;
                _lastErrorMessage = "Groq API key is not configured.";
                throw new InvalidOperationException(_lastErrorMessage);
            }

            try
            {
                var payload = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.5
                };

                var response = await _httpClient.PostAsJsonAsync("chat/completions", payload);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<GroqResponse>();
                var content = result?.Choices?[0]?.Message?.Content ?? string.Empty;

                if (result?.Usage != null)
                {
                    _lastPromptTokens = result.Usage.PromptTokens;
                    _lastCompletionTokens = result.Usage.CompletionTokens;
                    _lastTotalTokens = result.Usage.TotalTokens;
                    _lastWasActualTokenUsage = true;
                }
                else
                {
                    _lastPromptTokens = EstimateTokens(prompt);
                    _lastCompletionTokens = EstimateTokens(content);
                    _lastTotalTokens = _lastPromptTokens + _lastCompletionTokens;
                }

                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Groq API call failed.");
                _lastIsProviderFallback = true;
                _lastErrorMessage = ex.Message;
                throw new InvalidOperationException("Groq API call failed.", ex);
            }
        }
        public async Task<LlmAnswerResult> GenerateAnswerWithUsageAsync(string prompt)
        {
            var content = await GenerateAnswerAsync(prompt);
            return new LlmAnswerResult(
                content,
                _model,
                _lastPromptTokens,
                _lastCompletionTokens,
                _lastTotalTokens,
                _lastWasActualTokenUsage,
                !_lastIsProviderFallback,
                _lastIsProviderFallback,
                _lastErrorMessage);
        }

        public async IAsyncEnumerable<string> GenerateAnswerStreamAsync(string prompt)
        {
            _lastPromptTokens = 0;
            _lastCompletionTokens = 0;
            _lastTotalTokens = 0;
            _lastWasActualTokenUsage = false;
            _lastIsProviderFallback = false;
            _lastErrorMessage = null;

            if (!_hasApiKey)
            {
                _lastIsProviderFallback = true;
                _lastErrorMessage = "Groq API key is not configured.";
                throw new InvalidOperationException(_lastErrorMessage);
            }

            HttpResponseMessage? response = null;
            var accumulatedText = new System.Text.StringBuilder();
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
                    stream = true,
                    stream_options = new { include_usage = true }
                };

                // ResponseHeadersRead is required for true SSE streaming. PostAsJsonAsync
                // uses ResponseContentRead and buffers the whole completion first.
                using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                {
                    Content = JsonContent.Create(payload)
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    CancellationToken.None);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Groq streaming API call failed.");
                _lastIsProviderFallback = true;
                _lastErrorMessage = ex.Message;
                throw new InvalidOperationException("Groq streaming API call failed.", ex);
            }

            try
            {
                using var stream = await response!.Content.ReadAsStreamAsync();
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

                        if (chunk?.Usage != null)
                        {
                            _lastPromptTokens = chunk.Usage.PromptTokens;
                            _lastCompletionTokens = chunk.Usage.CompletionTokens;
                            _lastTotalTokens = chunk.Usage.TotalTokens;
                            _lastWasActualTokenUsage = true;
                        }

                        // The final usage frame from Groq legitimately has an empty choices array.
                        var text = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
                        if (!string.IsNullOrEmpty(text))
                        {
                            accumulatedText.Append(text);
                            yield return text;
                        }
                    }
                }

                // Fallback if Usage wasn't provided in the stream
                if (_lastTotalTokens == 0)
                {
                    _lastPromptTokens = EstimateTokens(prompt);
                    _lastCompletionTokens = EstimateTokens(accumulatedText.ToString());
                    _lastTotalTokens = _lastPromptTokens + _lastCompletionTokens;
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

        private static int EstimateTokens(string value)
        {
            return Math.Max(0, (int)Math.Ceiling((value?.Length ?? 0) / 4.0));
        }

        // Các class để deserialize phản hồi từ Groq API
        private class UsageInfo
        {
            [JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }

            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }
        }

        private class GroqResponse
        {
            [JsonPropertyName("choices")]
            public Choice[]? Choices { get; set; }

            [JsonPropertyName("usage")]
            public UsageInfo? Usage { get; set; }
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

            [JsonPropertyName("usage")]
            public UsageInfo? Usage { get; set; }
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
