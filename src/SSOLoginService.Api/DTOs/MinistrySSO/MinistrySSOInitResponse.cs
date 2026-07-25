namespace SSOLoginService.Api.DTOs.MinistrySSO;

public class MinistrySSOInitResponse
{
    public string LoginUrl { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int StateExpiresIn { get; set; } = 300;
}
