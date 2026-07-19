using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SSOLoginService.Api.DTOs.Auth;
using SSOLoginService.Api.DTOs.Common;
using SSOLoginService.Api.Services.Interfaces;

namespace SSOLoginService.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("AuthRateLimit")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IEnumerable<ISSOProvider> _ssoProviders;
    private readonly IOtpService _otpService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAuthService authService,
        IEnumerable<ISSOProvider> ssoProviders,
        IOtpService otpService,
        ITokenService tokenService,
        ILogger<AuthController> logger,
        IConfiguration configuration)
    {
        _authService = authService;
        _ssoProviders = ssoProviders;
        _otpService = otpService;
        _tokenService = tokenService;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated"));

        var userInfo = await _authService.GetCurrentUserAsync(userId);

        if (userInfo == null)
            return NotFound(ApiResponse<object>.Fail("User not found"));

        return Ok(ApiResponse<object>.Ok(userInfo));
    }

    [HttpGet("login")]
    public async Task<IActionResult> RedirectToSSO([FromQuery] string? provider = null)
    {
        var ssoProvider = ResolveProvider(provider);
        if (ssoProvider == null)
            return BadRequest(ApiResponse<object>.Fail("SSO provider not found"));

        var state = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var callbackUrl = $"{Request.Scheme}://{Request.Host}/sso/callback?provider={ssoProvider.ProviderType.ToString().ToLower()}";
        var loginUrl = await ssoProvider.GetAuthorizationUrlAsync(state, callbackUrl);

        HttpContext.Session.SetString("LoginState", state);
        HttpContext.Session.SetString("SSOProvider", ssoProvider.ProviderType.ToString());

        return Redirect(loginUrl);
    }

    [HttpPost("login/initiate")]
    public async Task<IActionResult> InitiateLogin([FromBody] LoginInitiateRequest? request)
    {
        var ssoProvider = ResolveProvider("moi");
        if (ssoProvider == null)
            return BadRequest(ApiResponse<object>.Fail("SSO provider not found"));

        var state = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var callbackUrl = $"{Request.Scheme}://{Request.Host}/sso/callback?provider={ssoProvider.ProviderType.ToString().ToLower()}";
        var loginUrl = await ssoProvider.GetAuthorizationUrlAsync(state, callbackUrl);

        HttpContext.Session.SetString("LoginState", state);
        HttpContext.Session.SetString("SSOProvider", ssoProvider.ProviderType.ToString());
        HttpContext.Session.SetString("ReturnUrl", request?.ReturnUrl ?? "/");

        return Ok(ApiResponse<LoginInitiateResponse>.Ok(
            new LoginInitiateResponse
            {
                LoginUrl = loginUrl,
                State = state
            }));
    }

    [HttpPost("second-login")]
    public async Task<IActionResult> SecondLogin([FromBody] SecondLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MelliCode))
            return BadRequest(ApiResponse<object>.Fail("کد ملی وارد نشده است"));

        var phones = await _authService.GetUserPhonesAsync(request.MelliCode);

        if (phones.Count == 0)
        {
            return Ok(ApiResponse<object>.Ok(new
            {
                exists = false,
                message = "کاربر با این کد ملی یافت نشد. لطفاً از طریق سامانه احراز هویت وزارت کشور وارد شوید.",
                redirectToSSO = true
            }));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            exists = true,
            phones
        }));
    }

    [HttpPost("second-login/send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SecondLoginVerifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(ApiResponse<object>.Fail("شماره تلفن وارد نشده است"));

        try
        {
            var code = await _otpService.GenerateAndSendOtpAsync(
                request.PhoneNumber, request.MelliCode);

            if (_configuration.GetValue<bool>("Otp:ShowCodeInResponse"))
            {
                return Ok(ApiResponse<object>.Ok(new { code, message = "کد تایید ارسال شد" }));
            }

            return Ok(ApiResponse<object>.Ok(new { message = "کد تایید ارسال شد" }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("second-login/verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] SecondLoginVerifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.OtpCode))
            return BadRequest(ApiResponse<object>.Fail("شماره تلفن و کد تایید الزامی است"));

        if (string.IsNullOrWhiteSpace(request.MelliCode))
            return BadRequest(ApiResponse<object>.Fail("کد ملی الزامی است"));

        var isValid = await _otpService.VerifyOtpAsync(
            request.PhoneNumber, request.OtpCode, request.MelliCode);

        if (!isValid)
            return Unauthorized(ApiResponse<object>.Fail("کد تایید نامعتبر یا منقضی شده است"));

        var result = await _authService.ProcessOtpLoginAsync(
            request.PhoneNumber, request.MelliCode);

        SetRefreshTokenCookie(result.RefreshToken, 30 * 24 * 60 * 60);

        return Ok(ApiResponse<UserTokenResponse>.Ok(result, "ورود با موفقیت انجام شد"));
    }

    [HttpGet("user-info")]
    [Authorize]
    public async Task<IActionResult> GetUserInfo()
    {
        return await GetCurrentUser();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Unauthorized(ApiResponse<object>.Fail("Refresh Token یافت نشد"));

        var result = await _tokenService.RefreshTokenAsync(refreshToken);
        if (result == null)
            return Unauthorized(ApiResponse<object>.Fail("Refresh Token نامعتبر است"));

        SetRefreshTokenCookie(result.RefreshToken, 30 * 24 * 60 * 60);

        return Ok(ApiResponse<object>.Ok(new
        {
            accessToken = result.AccessToken,
            expiresIn = result.ExpiresIn,
            tokenType = "Bearer"
        }));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            await _authService.LogoutAsync(userId);
        }

        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });

        return Ok(ApiResponse<object>.Ok(new { }, "خروج با موفقیت انجام شد"));
    }

    [HttpGet("validate-token")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateToken([FromQuery] string token)
    {
        var userId = await _tokenService.ValidateAccessTokenAsync(token);
        if (userId == null)
            return Ok(ApiResponse<object>.Fail("توکن نامعتبر است"));

        return Ok(ApiResponse<object>.Ok(new { userId = userId.Value }));
    }

    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var providers = _ssoProviders.Select(p => new
        {
            name = p.ProviderType.ToString().ToLower(),
            label = p.ProviderType switch
            {
                SSOProviderType.Moi => "پنجره ملی خدمات وزارت کشور",
                SSOProviderType.DolatMan => "دولت من",
                _ => p.ProviderType.ToString()
            },
            isActive = p.IsActive,
            loginUrl = p.IsActive
                ? Url.Action(nameof(RedirectToSSO), new { provider = p.ProviderType.ToString().ToLower() })
                : null
        });

        return Ok(ApiResponse<object>.Ok(providers));
    }

    private ISSOProvider? ResolveProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return _ssoProviders.FirstOrDefault(p => p.IsActive);

        if (Enum.TryParse<SSOProviderType>(provider, true, out var type))
            return _ssoProviders.FirstOrDefault(p => p.ProviderType == type && p.IsActive);

        return _ssoProviders.FirstOrDefault(p => p.IsActive);
    }

    private void SetRefreshTokenCookie(string refreshToken, int maxAgeInSeconds)
    {
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddSeconds(maxAgeInSeconds),
            Path = "/",
            MaxAge = TimeSpan.FromSeconds(maxAgeInSeconds)
        });
    }
}
