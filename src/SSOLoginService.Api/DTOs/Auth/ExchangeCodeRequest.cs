namespace SSOLoginService.Api.DTOs.Auth;

public class ExchangeCodeRequest
{
    public string Provider { get; set; } = "moi";
    public string Code { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}
