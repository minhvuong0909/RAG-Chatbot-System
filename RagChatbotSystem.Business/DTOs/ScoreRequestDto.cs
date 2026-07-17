using System.Text.Json.Serialization;

namespace RagChatbotSystem.Business.DTOs
{
    public class ScoreRequestDto
    {
        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonPropertyName("context")]
        public string Context { get; set; } = string.Empty;

        [JsonPropertyName("question")]
        public string Question { get; set; } = string.Empty;
    }
}
