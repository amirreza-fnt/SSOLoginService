namespace SSOLoginService.Api.DTOs.Auth;

public class UserTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public UserInfoDto User { get; set; } = new();
}

public class UserInfoDto
{
    public string Id { get; set; } = string.Empty;
    public string MelliCode { get; set; } = string.Empty;
    public string? Phone { get; set; }
}
