using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RagChatbotSystem.Business.DTOs
{
    public class IndexResponseDto
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("embeddings")]
        public List<float[]> Embeddings { get; set; } = new List<float[]>();
    }
}
