using System.Text.Json.Serialization;

namespace RagChatbotSystem.Business.DTOs
{
    public class RetrieveRequestDto
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("top_k")]
        public int TopK { get; set; } = 3;

        [JsonPropertyName("semantic_weight")]
        public double SemanticWeight { get; set; } = 0.7;

        [JsonPropertyName("lexical_weight")]
        public double LexicalWeight { get; set; } = 0.3;

        [JsonPropertyName("enable_rerank")]
        public bool EnableRerank { get; set; } = true;
    }
}
