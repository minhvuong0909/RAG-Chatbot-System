using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RagChatbotSystem.Business.DTOs
{
    public class RetrieveResponseDto
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("documents")]
        public List<DocumentModelDto> Documents { get; set; } = new List<DocumentModelDto>();

        [JsonPropertyName("scores")]
        public List<double> Scores { get; set; } = new List<double>();

        [JsonPropertyName("trace")]
        public List<string> Trace { get; set; } = new List<string>();
    }
}
