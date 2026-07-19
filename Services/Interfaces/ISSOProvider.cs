using SSOLoginService.Api.DTOs.MinistrySSO;

namespace SSOLoginService.Api.Services.Interfaces;

public enum SSOProviderType
{
    Moi,
    DolatMan
}

public record SSOCallbackResult
{
    public MinistrySSOUserInfo? UserInfo { get; init; }
    public string? Error { get; init; }
    public bool IsSuccess => UserInfo != null;
}

public interface ISSOProvider
{
    SSOProviderType ProviderType { get; }
    bool IsActive { get; }
    Task<string> GetAuthorizationUrlAsync(string state, string? callbackUrl = null);
    Task<SSOCallbackResult> HandleCallbackAsync(IQueryCollection query);
}
