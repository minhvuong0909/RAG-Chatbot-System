using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Business.Services
{
    public class RagApiClient : IRagApiClient
    {
        private readonly HttpClient _httpClient;

        public RagApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IndexResponseDto> IndexDocumentsAsync(IndexRequestDto request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/index", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<IndexResponseDto>();
                return result ?? new IndexResponseDto();
            }
            catch (Exception ex)
            {
                // Trong môi trường thực tế, hãy ghi log lỗi này
                Console.WriteLine($"Error calling Python /index: {ex.Message}");
                return new IndexResponseDto { Message = $"Error: {ex.Message}" };
            }
        }

        public async Task<RetrieveResponseDto> RetrieveAsync(RetrieveRequestDto request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/retrieve", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<RetrieveResponseDto>();
                return result ?? new RetrieveResponseDto();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Python /retrieve: {ex.Message}");
                return new RetrieveResponseDto { Query = request.Query };
            }
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
                Console.WriteLine($"Error calling Python delete API: {ex.Message}");
                return false;
            }
        }
    }
}
