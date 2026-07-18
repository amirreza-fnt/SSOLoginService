using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SSOLoginService.Api.DTOs.Common;
using SSOLoginService.Api.DTOs.MinistrySSO;
using SSOLoginService.Api.Services.Interfaces;

namespace SSOLoginService.Api.Controllers;

[ApiController]
[EnableRateLimiting("AuthRateLimit")]
[Produces("application/json")]
public class SSOCallbackController : ControllerBase
{
    private readonly IMinistrySSOService _ministrySsoService;
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<SSOCallbackController> _logger;
    private readonly IConfiguration _configuration;
    private readonly ISmsService _smsService;

    public SSOCallbackController(
        IMinistrySSOService ministrySsoService,
        IAuthService authService,
        ITokenService tokenService,
        ILogger<SSOCallbackController> logger,
        IConfiguration configuration,
        ISmsService smsService)
    {
        _ministrySsoService = ministrySsoService;
        _authService = authService;
        _tokenService = tokenService;
        _logger = logger;
        _configuration = configuration;
        _smsService = smsService;
    }

    [HttpGet("/sso/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> SSOCallback()
    {
        try
        {
            _logger.LogInformation("SSO callback received: {QueryString}", Request.QueryString.Value);

            var fullQueryString = Request.QueryString.Value?.TrimStart('?');
            if (string.IsNullOrWhiteSpace(fullQueryString))
                return RedirectToClientWithError("no_data", "اطلاعات احراز هویت دریافت نشد");

            byte[] jsonBytes;
            try
            {
                jsonBytes = Convert.FromBase64String(fullQueryString);
            }
            catch
            {
                return RedirectToClientWithError("invalid_format", "فرمت داده ورودی نامعتبر است");
            }

            var jsonData = Encoding.UTF8.GetString(jsonBytes);
            _logger.LogInformation("Decoded JSON: {Json}", jsonData);

            MinistrySSOCallbackPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<MinistrySSOCallbackPayload>(jsonData);
            }
            catch
            {
                return RedirectToClientWithError("invalid_json", "فرمت JSON نامعتبر است");
            }

            if (payload == null || !payload.Status)
                return RedirectToClientWithError("invalid_data", "اطلاعات احراز هویت نامعتبر است");

            if (string.IsNullOrWhiteSpace(payload.Data))
                return RedirectToClientWithError("no_data", "داده رمزنگاری شده یافت نشد");

            if (payload.IvKey.Length == 0)
                return RedirectToClientWithError("no_iv", "کلید رمزنگاری یافت نشد");

            var appSecret = _configuration["MinistrySSO:AppSecret"];
            if (string.IsNullOrWhiteSpace(appSecret))
                return RedirectToClientWithError("config_error", "تنظیمات SSO یافت نشد");

            var userInfo = await _ministrySsoService.DecryptAndExtractUserInfoAsync(
                payload.Data, payload.IvKey, appSecret);

            if (userInfo == null)
                return RedirectToClientWithError("decrypt_error", "خطا در رمزگشایی اطلاعات کاربر");

            if (string.IsNullOrWhiteSpace(userInfo.MelliCode))
                return RedirectToClientWithError("missing_melli_code", "کد ملی در اطلاعات SSO یافت نشد");

            var result = await _authService.ProcessMinistrySSOLoginAsync(userInfo);

            var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            var callbackUrl = $"{frontendUrl}/auth/callback?token={result.AccessToken}&refreshToken={result.RefreshToken}";

            _logger.LogInformation("Redirecting to frontend: {CallbackUrl}", callbackUrl);
            return Redirect(callbackUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in SSO callback");
            return RedirectToClientWithError("server_error", "خطای داخلی سرور");
        }
    }

    private IActionResult RedirectToClientWithError(string errorCode, string errorMessage)
    {
        var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
        var errorUrl = $"{frontendUrl}/auth/error?error={errorCode}&message={Uri.EscapeDataString(errorMessage)}";
        _logger.LogWarning("Redirecting to error: {ErrorUrl}", errorUrl);
        return Redirect(errorUrl);
    }
}
