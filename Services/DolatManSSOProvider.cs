using SSOLoginService.Api.DTOs.MinistrySSO;
using SSOLoginService.Api.Services.Interfaces;

namespace SSOLoginService.Api.Services;

public class DolatManSSOProvider : ISSOProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DolatManSSOProvider> _logger;

    public SSOProviderType ProviderType => SSOProviderType.DolatMan;

    public bool IsActive => _configuration.GetValue<bool>($"SSO:{ProviderType}:Enabled");

    public DolatManSSOProvider(
        IConfiguration configuration,
        ILogger<DolatManSSOProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<string> GetAuthorizationUrlAsync(string state, string? callbackUrl = null)
    {
        var config = GetConfig();
        if (!IsActive || string.IsNullOrWhiteSpace(config.ClientId))
            throw new InvalidOperationException("پروایدر دولت من فعال نیست");

        var redirectUri = callbackUrl ?? config.CallbackUrl;
        var loginUrl = $"{config.BaseUrl}/oauth2/authorize" +
                       $"?response_type=code" +
                       $"&scope=openid%20profile" +
                       $"&client_id={config.ClientId}" +
                       $"&state={state}" +
                       $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                       $"&nonce={Guid.NewGuid():N}";

        return Task.FromResult(loginUrl);
    }

    public Task<SSOCallbackResult> HandleCallbackAsync(IQueryCollection query)
    {
        if (!IsActive)
            return Task.FromResult(new SSOCallbackResult { Error = "پروایدر دولت من فعال نیست" });

        _logger.LogInformation("DolatMan SSO callback received: {Query}", query);

        // TODO: پیاده‌سازی کامل پس از فعال شدن دولت من
        // مشابه MoiSSOProvider:
        // 1. دریافت code از query
        // 2. مبادله code با token از طریق POST /oauth2/token (Basic Auth)
        // 3. دریافت userinfo با Bearer token
        // 4. برگرداندن MinistrySSOUserInfo

        return Task.FromResult(new SSOCallbackResult { Error = "پروایدر دولت من هنوز فعال نشده است" });
    }

    private DolatManConfig GetConfig()
    {
        var section = _configuration.GetSection($"SSO:{ProviderType}");
        return new DolatManConfig
        {
            BaseUrl = section["BaseUrl"] ?? "https://sso.dolat.ir",
            ClientId = section["ClientId"] ?? "",
            ClientSecret = section["ClientSecret"] ?? "",
            CallbackUrl = section["CallbackUrl"] ?? ""
        };
    }

    private record DolatManConfig
    {
        public string BaseUrl { get; init; } = string.Empty;
        public string ClientId { get; init; } = string.Empty;
        public string ClientSecret { get; init; } = string.Empty;
        public string CallbackUrl { get; init; } = string.Empty;
    }
}
