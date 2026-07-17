using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RagChatbotSystem.Business.DTOs
{
    public class IndexRequestDto
    {
        [JsonPropertyName("documents")]
        public List<DocumentModelDto> Documents { get; set; } = new List<DocumentModelDto>();

        [JsonPropertyName("rebuild_cache")]
        public bool RebuildCache { get; set; } = false;

        [JsonPropertyName("profile_id")]
        public string ProfileId { get; set; } = "default";
    }
}
