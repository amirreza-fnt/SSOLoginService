using System.Text.Json.Serialization;

namespace SSOLoginService.Api.DTOs.MinistrySSO;

public class MinistrySSOUserInfo
{
    [JsonPropertyName("MelliCode")]
    public string? MelliCode { get; set; }

    [JsonPropertyName("FirstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("LastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("FatherName")]
    public string? FatherName { get; set; }

    [JsonPropertyName("BirthDate")]
    public string? BirthDate { get; set; }

    [JsonPropertyName("Gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("Address")]
    public string? Address { get; set; }

    [JsonPropertyName("Mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("PostalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("Province")]
    public string? Province { get; set; }

    [JsonPropertyName("City")]
    public string? City { get; set; }

    [JsonPropertyName("Email")]
    public string? Email { get; set; }

    [JsonPropertyName("Avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("PhoneList")]
    public string? PhoneList { get; set; }

    /// <summary>Optional municipal / MOI roles (e.g. ROLE_OPERATOR).</summary>
    [JsonPropertyName("Roles")]
    public List<string>? Roles { get; set; }

    /// <summary>Optional municipal group id (e.g. EXECUTION).</summary>
    [JsonPropertyName("GroupId")]
    public string? GroupId { get; set; }
}
