using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RagChatbotSystem.Business.DTOs
{
    public class DocumentModelDto
    {
        [JsonPropertyName("page_content")]
        public string PageContent { get; set; } = string.Empty;

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
