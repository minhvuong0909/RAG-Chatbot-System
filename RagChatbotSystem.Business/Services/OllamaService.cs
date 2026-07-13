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
    public class OllamaService : ILlmService
    {
        private readonly HttpClient _httpClient;
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

        public OllamaService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _model = configuration["Ollama:Model"] ?? "llama3";
        }

        public async Task<string> GenerateAnswerAsync(string prompt)
        {
            _lastPromptTokens = 0;
            _lastCompletionTokens = 0;
            _lastTotalTokens = 0;
            _lastWasActualTokenUsage = false;
            _lastIsProviderFallback = false;
            _lastErrorMessage = null;

            try
            {
                var payload = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    stream = false
                };

                var response = await _httpClient.PostAsJsonAsync("api/chat", payload);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                var content = result?.Message?.Content ?? string.Empty;

                if (result?.PromptEvalCount != null || result?.EvalCount != null)
                {
                    _lastPromptTokens = result.PromptEvalCount ?? 0;
                    _lastCompletionTokens = result.EvalCount ?? 0;
                    _lastTotalTokens = _lastPromptTokens + _lastCompletionTokens;
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
                Console.WriteLine($"Error calling Ollama API: {ex.Message}");
                _lastIsProviderFallback = true;
                _lastErrorMessage = ex.Message;
                throw new InvalidOperationException(
                    "Ollama API call failed. Ensure Ollama is running locally (ollama serve) and the model is pulled (ollama pull " + _model + ").",
                    ex);
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
                    stream = true
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "api/chat")
                {
                    Content = JsonContent.Create(payload)
                };
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error streaming Ollama API: {ex.Message}");
                _lastIsProviderFallback = true;
                _lastErrorMessage = ex.Message;
                throw new InvalidOperationException(
                    "Ollama streaming API call failed. Ensure Ollama is running locally (ollama serve) and the model is pulled (ollama pull " + _model + ").",
                    ex);
            }

            try
            {
                using var stream = await response!.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    OllamaResponse? chunk = null;
                    try
                    {
                        chunk = System.Text.Json.JsonSerializer.Deserialize<OllamaResponse>(line);
                    }
                    catch
                    {
                        // Ignore malformed json in stream
                    }

                    var text = chunk?.Message?.Content;
                    if (!string.IsNullOrEmpty(text))
                    {
                        accumulatedText.Append(text);
                        yield return text;
                    }

                    if (chunk?.Done == true && (chunk.PromptEvalCount != null || chunk.EvalCount != null))
                    {
                        _lastPromptTokens = chunk.PromptEvalCount ?? 0;
                        _lastCompletionTokens = chunk.EvalCount ?? 0;
                        _lastTotalTokens = _lastPromptTokens + _lastCompletionTokens;
                        _lastWasActualTokenUsage = true;
                    }
                }

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

        private static int EstimateTokens(string value)
        {
            return Math.Max(0, (int)Math.Ceiling((value?.Length ?? 0) / 4.0));
        }

        private class OllamaResponse
        {
            [JsonPropertyName("message")]
            public OllamaMessage? Message { get; set; }

            [JsonPropertyName("done")]
            public bool Done { get; set; }

            [JsonPropertyName("prompt_eval_count")]
            public int? PromptEvalCount { get; set; }

            [JsonPropertyName("eval_count")]
            public int? EvalCount { get; set; }
        }

        private class OllamaMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }
    }
}
