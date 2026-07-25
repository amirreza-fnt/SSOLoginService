using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSOLoginService.Api.DTOs.Common;
using SSOLoginService.Api.Services.Interfaces;

namespace SSOLoginService.Api.Controllers;

[ApiController]
[Route("api/token")]
[Produces("application/json")]
public class TokenController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<TokenController> _logger;

    public TokenController(
        ITokenService tokenService,
        ILogger<TokenController> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
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

    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized(ApiResponse<object>.Fail("User not authenticated"));

        await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        _logger.LogInformation("Token revoked by user {UserId}", userIdClaim);

        return Ok(ApiResponse<object>.Ok(new { }, "توکن با موفقیت باطل شد"));
    }

    [HttpGet("introspect")]
    [Authorize]
    public async Task<IActionResult> IntrospectToken()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return Unauthorized(ApiResponse<object>.Fail("No token provided"));

        var token = authHeader["Bearer ".Length..].Trim();
        var userId = await _tokenService.ValidateAccessTokenAsync(token);

        if (userId == null)
            return Ok(ApiResponse<object>.Ok(new { active = false }));

        return Ok(ApiResponse<object>.Ok(new
        {
            active = true,
            userId = userId.Value
        }));
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

public class RevokeTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
