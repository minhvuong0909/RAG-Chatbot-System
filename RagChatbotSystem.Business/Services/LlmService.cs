using System;
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
                // Fallback mock response for easy local testing without API key
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
                
                // Try to extract useful information from prompt context to show a simulated answer on API failure
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
