using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Business.Services
{
    public class LlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public LlmService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        }

        public async Task<string> GenerateAnswerAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return $"[MOCK ANSWER - Please configure Gemini:ApiKey in appsettings.json]\n\n" +
                       $"Dựa vào các tài liệu tìm thấy, đây là câu trả lời thử nghiệm cho câu hỏi của bạn. Hệ thống RAG đã tìm thấy các đoạn trích liên quan và đang đợi khóa API của bạn hoạt động để tạo ra câu trả lời AI hoàn chỉnh.\n\n" +
                       $"Nội dung câu hỏi của bạn: {prompt.Substring(0, Math.Min(prompt.Length, 100))}...";
            }

            try
            {
                var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}";
                
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
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
                
                if (result?.Candidates != null && result.Candidates.Length > 0 && 
                    result.Candidates[0].Content?.Parts != null && result.Candidates[0].Content.Parts.Length > 0)
                {
                    return result.Candidates[0].Content.Parts[0].Text ?? string.Empty;
                }

                return "Không thể nhận diện được câu trả lời từ AI model.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Gemini API: {ex.Message}");
                
                if (prompt.Contains("Ngữ cảnh:") && prompt.Contains("Câu hỏi:"))
                {
                    var contextStart = prompt.IndexOf("Ngữ cảnh:\n") + 10;
                    var contextEnd = prompt.IndexOf("\n\nCâu hỏi:");
                    if (contextEnd > contextStart)
                    {
                        var context = prompt.Substring(contextStart, contextEnd - contextStart);
                        return $"[LƯU Ý: Lỗi kết nối Gemini API ({ex.Message}). Câu trả lời được trích xuất trực tiếp từ tài liệu của bạn]:\n\n{context.Trim()}";
                    }
                }
                return $"Lỗi khi kết nối với AI (Gemini): {ex.Message}";
            }
        }

        public async IAsyncEnumerable<string> GenerateAnswerStreamAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                var mockText = $"[MOCK ANSWER - Please configure Gemini:ApiKey in appsettings.json]\n\n" +
                               $"Dựa vào các tài liệu tìm thấy, đây là câu trả lời thử nghiệm cho câu hỏi của bạn. Hệ thống RAG đã tìm thấy các đoạn trích liên quan và đang đợi khóa API của bạn hoạt động để tạo ra câu trả lời AI hoàn chỉnh.";
                var words = mockText.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    yield return word + " ";
                    await Task.Delay(30);
                }
                yield break;
            }

            HttpResponseMessage? response = null;
            try
            {
                var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:streamGenerateContent?key={_apiKey}";
                
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
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);

                // Gemini stream format is a JSON array of candidate objects or a stream of JSON objects
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var trimmed = line.Trim();
                    // Strip JSON array markers if present
                    if (trimmed.StartsWith("[")) trimmed = trimmed.Substring(1);
                    if (trimmed.EndsWith("]")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
                    if (trimmed.StartsWith(",")) trimmed = trimmed.Substring(1);
                    trimmed = trimmed.Trim();

                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    GeminiResponse? chunk = null;
                    try
                    {
                        chunk = System.Text.Json.JsonSerializer.Deserialize<GeminiResponse>(trimmed);
                    }
                    catch
                    {
                        // Ignore partial JSON parsing issues
                    }

                    if (chunk?.Candidates != null && chunk.Candidates.Length > 0 &&
                        chunk.Candidates[0].Content?.Parts != null && chunk.Candidates[0].Content.Parts.Length > 0)
                    {
                        var text = chunk.Candidates[0].Content.Parts[0].Text;
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
        }

        private class GeminiCandidate
        {
            [JsonPropertyName("content")]
            public GeminiContent Content { get; set; } = null!;
        }
        #endregion
    }
}
