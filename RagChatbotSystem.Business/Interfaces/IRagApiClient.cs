using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IRagApiClient
    {
        Task<IndexResponseDto> IndexDocumentsAsync(IndexRequestDto request);
        Task<RetrieveResponseDto> RetrieveAsync(RetrieveRequestDto request);
        Task<bool> DeleteDocumentAsync(System.Guid documentId);

        /// <summary>
        /// Chấm điểm câu trả lời bằng embedding cosine (chuẩn RAGAS): faithfulness + relevance.
        /// Best-effort: trả về null nếu RAG API lỗi (không làm sập luồng so sánh).
        /// </summary>
        Task<ScoreResponseDto?> ScoreAsync(ScoreRequestDto request);
    }
}
