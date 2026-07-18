namespace SSOLoginService.Api.DTOs.Auth;

public class LoginInitiateResponse
{
    public string LoginUrl { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}
