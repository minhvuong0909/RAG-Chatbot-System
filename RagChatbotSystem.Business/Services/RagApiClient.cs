using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Business.Services
{
    public class RagApiClient : IRagApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RagApiClient> _logger;

        public RagApiClient(HttpClient httpClient, ILogger<RagApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<IndexResponseDto> IndexDocumentsAsync(IndexRequestDto request)
        {
            var response = await _httpClient.PostAsJsonAsync("/index", request);
            await EnsureSuccessAsync(response, "/index");

            var result = await response.Content.ReadFromJsonAsync<IndexResponseDto>();
            return result ?? throw new InvalidOperationException("Python /index returned an empty response body.");
        }

        public async Task<RetrieveResponseDto> RetrieveAsync(RetrieveRequestDto request)
        {
            var response = await _httpClient.PostAsJsonAsync("/retrieve", request);
            await EnsureSuccessAsync(response, "/retrieve");

            var result = await response.Content.ReadFromJsonAsync<RetrieveResponseDto>();
            return result ?? throw new InvalidOperationException("Python /retrieve returned an empty response body.");
        }

        public async Task<bool> DeleteDocumentAsync(Guid documentId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/documents/{documentId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete document {DocumentId} from Python RAG index.", documentId);
                return false;
            }
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response, string endpoint)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Python RAG API {endpoint} failed with {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }
    }
}
