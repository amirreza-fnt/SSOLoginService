using System.Text.Json.Serialization;

namespace SSOLoginService.Api.DTOs.MinistrySSO;

public class MoiTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("id_token")]
    public string IdToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

public class MoiUserInfoResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nationalId")]
    public string? NationalId { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("fatherName")]
    public string? FatherName { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("birthDate")]
    public string? BirthDate { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("province")]
    public string? Province { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("address")]
    public MoiAddressInfo? Address { get; set; }

    [JsonPropertyName("birthDateShamsi")]
    public string? BirthDateShamsi { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("phoneList")]
    public string? PhoneList { get; set; }

    [JsonPropertyName("roles")]
    public List<string>? Roles { get; set; }

    [JsonPropertyName("married")]
    public bool? Married { get; set; }

    [JsonPropertyName("shaba")]
    public string? Shaba { get; set; }

    [JsonPropertyName("shenasnamehNo")]
    public string? ShenasnamehNo { get; set; }
}

public class MoiAddressInfo
{
    [JsonPropertyName("province")]
    public string? Province { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("street")]
    public string? Street { get; set; }

    [JsonPropertyName("postCode")]
    public string? PostCode { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
