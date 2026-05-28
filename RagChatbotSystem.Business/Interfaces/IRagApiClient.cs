using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IRagApiClient
    {
        Task<IndexResponseDto> IndexDocumentsAsync(IndexRequestDto request);
        Task<RetrieveResponseDto> RetrieveAsync(RetrieveRequestDto request);
        Task<bool> DeleteDocumentAsync(System.Guid documentId);
    }
}
