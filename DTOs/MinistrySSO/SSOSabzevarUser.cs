using System.Text.Json.Serialization;

namespace SSOLoginService.Api.DTOs.MinistrySSO;

public class SSOSabzevarUser
{
    [JsonPropertyName("MelliCode")]
    public string? MelliCode { get; set; }

    [JsonPropertyName("FirstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("LastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("Address")]
    public string? Address { get; set; }

    [JsonPropertyName("Mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("LoggedMobile")]
    public string? LoggedMobile { get; set; }

    [JsonPropertyName("UserID")]
    public string? UserID { get; set; }

    [JsonPropertyName("IsManager")]
    public bool IsManager { get; set; }
}
