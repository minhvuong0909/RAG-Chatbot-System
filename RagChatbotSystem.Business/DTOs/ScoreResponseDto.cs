using System.Text.Json.Serialization;

namespace RagChatbotSystem.Business.DTOs
{
    public class ScoreResponseDto
    {
        // Cosine similarity (0..1) — chuẩn RAGAS.
        [JsonPropertyName("faithfulness")]
        public double Faithfulness { get; set; }

        [JsonPropertyName("relevance")]
        public double Relevance { get; set; }
    }
}
