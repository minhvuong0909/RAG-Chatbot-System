using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RagChatbotSystem.Business.DTOs;
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
            _lastWasActualTokenUsage = false;
            _lastIsProviderFallback = false;
            _lastErrorMessage = null;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _lastIsProviderFallback = true;
                _lastErrorMessage = "OpenAI API key is not configured.";
                throw new InvalidOperationException(_lastErrorMessage);
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
                Console.WriteLine($"Error calling OpenAI API: {ex.Message}");
                _lastIsProviderFallback = true;
                _lastErrorMessage = ex.Message;
                throw new InvalidOperationException("OpenAI API call failed.", ex);
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

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _lastIsProviderFallback = true;
                _lastErrorMessage = "OpenAI API key is not configured.";
                throw new InvalidOperationException(_lastErrorMessage);
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error streaming OpenAI API: {ex.Message}");
                _lastIsProviderFallback = true;
                _lastErrorMessage = ex.Message;
                throw new InvalidOperationException("OpenAI streaming API call failed.", ex);
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

                        OpenAiStreamResponse? chunk = null;
                        try
                        {
                            chunk = System.Text.Json.JsonSerializer.Deserialize<OpenAiStreamResponse>(data);
                        }
                        catch
                        {
                            // Ignore json deserialization issues on stream progress
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

                _lastPromptTokens = EstimateTokens(prompt);
                _lastCompletionTokens = EstimateTokens(accumulatedText.ToString());
                _lastTotalTokens = _lastPromptTokens + _lastCompletionTokens;
            }
        }
        private static int EstimateTokens(string value)
        {
            return Math.Max(0, (int)Math.Ceiling((value?.Length ?? 0) / 4.0));
        }

        private class OpenAiResponse
        {
            [JsonPropertyName("choices")]
            public Choice[] Choices { get; set; } = Array.Empty<Choice>();

            [JsonPropertyName("usage")]
            public UsageInfo? Usage { get; set; }
        }

        private class UsageInfo
        {
            [JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }

            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }
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
