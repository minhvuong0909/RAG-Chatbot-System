using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Business.Services
{
    public class LlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string ModelName = "gemini-3.1-flash-lite";

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
        string ILlmService.ModelName => ModelName;

        public LlmService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        }

        public async Task<string> GenerateAnswerAsync(string prompt)
        {
            _lastPromptTokens = 0;
            _lastCompletionTokens = 0;
            _lastTotalTokens = 0;
            _lastWasActualTokenUsage = false;
            _lastIsProviderFallback = false;
            _lastErrorMessage = null;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _lastIsProviderFallback = true;
                _lastErrorMessage = "Gemini API key is not configured.";
                throw new InvalidOperationException(_lastErrorMessage);
            }

            try
            {
                var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:generateContent?key={_apiKey}";

                var payload = new GeminiRequest
                {
                    Contents = new[]
                    {
                        new GeminiContent
                        {
                            Parts = new[]
                            {
                                new GeminiPart { Text = prompt }
                            }
                        }
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(requestUrl, payload);
                await EnsureSuccessOrThrowAsync(response);

                var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
                var answer = ExtractText(result?.Candidates);

                if (result?.UsageMetadata != null)
                {
                    _lastPromptTokens = result.UsageMetadata.PromptTokenCount;
                    _lastCompletionTokens = result.UsageMetadata.CandidatesTokenCount;
                    _lastTotalTokens = result.UsageMetadata.TotalTokenCount;
                    _lastWasActualTokenUsage = true;
                }
                else
                {
                    _lastPromptTokens = EstimateTokens(prompt);
                    _lastCompletionTokens = EstimateTokens(answer);
                    _lastTotalTokens = _lastPromptTokens + _lastCompletionTokens;
                }

                return answer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Gemini API: {ex.Message}");
                _lastIsProviderFallback = true;
                _lastErrorMessage = ex.Message;
                throw new InvalidOperationException("Gemini API call failed.", ex);
            }
        }
        public async Task<LlmAnswerResult> GenerateAnswerWithUsageAsync(string prompt)
        {
            var content = await GenerateAnswerAsync(prompt);
            return new LlmAnswerResult(
                content,
                ModelName,
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

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _lastIsProviderFallback = true;
                _lastErrorMessage = "Gemini API key is not configured.";
                throw new InvalidOperationException(_lastErrorMessage);
            }

            HttpResponseMessage? response = null;
            var accumulatedText = new System.Text.StringBuilder();
            try
            {
                var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:streamGenerateContent?key={_apiKey}";

                var payload = new GeminiRequest
                {
                    Contents = new[]
                    {
                        new GeminiContent
                        {
                            Parts = new[]
                            {
                                new GeminiPart { Text = prompt }
                            }
                        }
                    }
                };

                response = await _httpClient.PostAsJsonAsync(requestUrl, payload);
                await EnsureSuccessOrThrowAsync(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error streaming Gemini API: {ex.Message}");
                _lastIsProviderFallback = true;
                _lastErrorMessage = ex.Message;
                throw new InvalidOperationException("Gemini streaming API call failed.", ex);
            }

            try
            {
                using var stream = await response!.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);

                // Gemini stream format is a JSON array of candidate objects or a stream of JSON objects.
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("[")) trimmed = trimmed.Substring(1);
                    if (trimmed.EndsWith("]")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
                    if (trimmed.EndsWith(",")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed == ",") continue;

                    GeminiResponse? chunk = null;
                    try
                    {
                        chunk = System.Text.Json.JsonSerializer.Deserialize<GeminiResponse>(trimmed);
                    }
                    catch
                    {
                        // Ignore partial JSON parsing issues
                    }

                    if (chunk?.UsageMetadata != null)
                    {
                        _lastPromptTokens = chunk.UsageMetadata.PromptTokenCount;
                        _lastCompletionTokens = chunk.UsageMetadata.CandidatesTokenCount;
                        _lastTotalTokens = chunk.UsageMetadata.TotalTokenCount;
                        _lastWasActualTokenUsage = true;
                    }

                    var text = ExtractText(chunk?.Candidates);
                    if (!string.IsNullOrEmpty(text))
                    {
                        accumulatedText.Append(text);
                        yield return text;
                    }
                }
            }
            finally
            {
                response?.Dispose();

                if (_lastTotalTokens == 0)
                {
                    _lastPromptTokens = EstimateTokens(prompt);
                    _lastCompletionTokens = EstimateTokens(accumulatedText.ToString());
                    _lastTotalTokens = _lastPromptTokens + _lastCompletionTokens;
                }
            }
        }
        private static int EstimateTokens(string value)
        {
            return Math.Max(0, (int)Math.Ceiling((value?.Length ?? 0) / 4.0));
        }

        // Gemini 3.x có thể tách câu trả lời thành nhiều "part" (hoặc part đầu là thought) — gộp
        // toàn bộ text của candidate đầu tiên thay vì chỉ lấy Parts[0] để không bị cụt/rỗng.
        private static string ExtractText(GeminiCandidate[]? candidates)
        {
            if (candidates == null || candidates.Length == 0) return string.Empty;

            var parts = candidates[0].Content?.Parts;
            if (parts == null || parts.Length == 0) return string.Empty;

            var sb = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (!string.IsNullOrEmpty(part.Text)) sb.Append(part.Text);
            }
            return sb.ToString();
        }

        // EnsureSuccessStatusCode chỉ trả về mã lỗi mà bỏ qua body — nơi Google ghi lý do thật
        // (sai key / hết quota / model không tồn tại...). Đọc kèm body để lỗi hiển thị có ích.
        private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;

            var body = await response.Content.ReadAsStringAsync();
            var detail = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body.Trim();
            if (!string.IsNullOrEmpty(detail) && detail.Length > 500)
            {
                detail = detail.Substring(0, 500) + "…";
            }
            throw new HttpRequestException($"Gemini API {(int)response.StatusCode} {response.StatusCode}: {detail}");
        }

        #region Gemini API JSON Serialization Classes
        private class GeminiRequest
        {
            [JsonPropertyName("contents")]
            public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();
        }

        private class GeminiContent
        {
            [JsonPropertyName("parts")]
            public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
        }

        private class GeminiPart
        {
            [JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;
        }

        private class GeminiResponse
        {
            [JsonPropertyName("candidates")]
            public GeminiCandidate[] Candidates { get; set; } = Array.Empty<GeminiCandidate>();

            [JsonPropertyName("usageMetadata")]
            public GeminiUsageMetadata? UsageMetadata { get; set; }
        }

        private class GeminiUsageMetadata
        {
            [JsonPropertyName("promptTokenCount")]
            public int PromptTokenCount { get; set; }

            [JsonPropertyName("candidatesTokenCount")]
            public int CandidatesTokenCount { get; set; }

            [JsonPropertyName("totalTokenCount")]
            public int TotalTokenCount { get; set; }
        }

        private class GeminiCandidate
        {
            [JsonPropertyName("content")]
            public GeminiContent Content { get; set; } = null!;
        }
        #endregion
    }
}
