using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SSOLoginService.Api.DTOs.Common;
using SSOLoginService.Api.Services.Interfaces;

namespace SSOLoginService.Api.Controllers;

[ApiController]
[EnableRateLimiting("AuthRateLimit")]
[Produces("application/json")]
public class SSOCallbackController : ControllerBase
{
    private readonly IEnumerable<ISSOProvider> _ssoProviders;
    private readonly IAuthService _authService;
    private readonly ILogger<SSOCallbackController> _logger;
    private readonly IConfiguration _configuration;

    public SSOCallbackController(
        IEnumerable<ISSOProvider> ssoProviders,
        IAuthService authService,
        ILogger<SSOCallbackController> logger,
        IConfiguration configuration)
    {
        _ssoProviders = ssoProviders;
        _authService = authService;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("/sso/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> SSOCallback(
        [FromQuery] string? provider = null,
        [FromQuery] string? code = null,
        [FromQuery] string? state = null)
    {
        try
        {
            _logger.LogInformation("SSO callback received: {QueryString}", Request.QueryString.Value);

            var ssoProvider = ResolveProvider(provider);
            if (ssoProvider == null)
            {
                _logger.LogWarning("No SSO provider found for: {Provider}", provider);
                return RedirectToClientWithError("invalid_provider", "پروایدر SSO نامعتبر است");
            }

            var result = await ssoProvider.HandleCallbackAsync(Request.Query);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("SSO callback failed: {Error}", result.Error);
                return RedirectToClientWithError("auth_failed", result.Error ?? "خطا در احراز هویت");
            }

            if (result.UserInfo == null || string.IsNullOrWhiteSpace(result.UserInfo.MelliCode))
                return RedirectToClientWithError("missing_melli_code", "کد ملی در اطلاعات SSO یافت نشد");

            _logger.LogInformation("SSO user authenticated: melliCode={MelliCode}, provider={Provider}",
                result.UserInfo.MelliCode, ssoProvider.ProviderType);

            var tokenResult = await _authService.ProcessMinistrySSOLoginAsync(result.UserInfo);

            var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            var callbackUrl = $"{frontendUrl}/auth/callback?token={tokenResult.AccessToken}&refreshToken={tokenResult.RefreshToken}";

            _logger.LogInformation("Redirecting to frontend: {CallbackUrl}", callbackUrl);
            return Redirect(callbackUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in SSO callback");
            return RedirectToClientWithError("server_error", "خطای داخلی سرور");
        }
    }

    private ISSOProvider? ResolveProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return _ssoProviders.FirstOrDefault();

        if (Enum.TryParse<SSOProviderType>(provider, true, out var type))
            return _ssoProviders.FirstOrDefault(p => p.ProviderType == type);

        return _ssoProviders.FirstOrDefault();
    }

    private IActionResult RedirectToClientWithError(string errorCode, string errorMessage)
    {
        var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
        var errorUrl = $"{frontendUrl}/auth/error?error={errorCode}&message={Uri.EscapeDataString(errorMessage)}";
        _logger.LogWarning("Redirecting to error: {ErrorUrl}", errorUrl);
        return Redirect(errorUrl);
    }
}
