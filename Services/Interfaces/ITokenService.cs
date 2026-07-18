using SSOLoginService.Api.Models;

namespace SSOLoginService.Api.Services.Interfaces;

public class TokenResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public User? User { get; set; }
}

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Task<TokenResult> GenerateTokensAsync(User user);
    Task<Guid?> ValidateAccessTokenAsync(string token);
    Task<TokenResult?> RefreshTokenAsync(string refreshToken);
    Task RevokeRefreshTokenAsync(string refreshToken);
    Guid? GetUserIdFromToken(string token);
}
