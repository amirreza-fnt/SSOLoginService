using System.Text.Json.Serialization;

namespace SSOLoginService.Api.DTOs.MinistrySSO;

public class MinistrySSOCallbackPayload
{
    [JsonPropertyName("Data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("AppID")]
    public Guid AppID { get; set; }

    [JsonPropertyName("Status")]
    public bool Status { get; set; }

    [JsonPropertyName("IvKey")]
    public byte[] IvKey { get; set; } = Array.Empty<byte>();

    [JsonPropertyName("DateCreated")]
    public DateTime DateCreated { get; set; }
}
