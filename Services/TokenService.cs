using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SSOLoginService.Api.Data;
using SSOLoginService.Api.Models;
using SSOLoginService.Api.Services.Interfaces;

namespace SSOLoginService.Api.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;
    private readonly ILogger<TokenService> _logger;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public TokenService(
        IConfiguration configuration,
        AppDbContext context,
        ILogger<TokenService> logger)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    public string GenerateAccessToken(User user)
    {
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? "DefaultSuperSecretKeyForSSOLoginService@1404!VeryLong";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var accessTokenExpiration = int.Parse(
            _configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new Claim("MelliCode", user.MelliCode),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("IsActive", user.IsActive.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "SSOLoginService",
            audience: _configuration["Jwt:Audience"] ?? "SSOLoginService.Client",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(accessTokenExpiration),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public async Task<TokenResult> GenerateTokensAsync(User user)
    {
        var accessToken = GenerateAccessToken(user);

        await _lock.WaitAsync();
        try
        {
            var oldTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == user.Id)
                .ToListAsync();

            if (oldTokens.Count != 0)
            {
                _context.RefreshTokens.RemoveRange(oldTokens);
                await _context.SaveChangesAsync();
            }

            string refreshToken;
            bool exists;
            var retry = 0;

            do
            {
                refreshToken = GenerateRefreshToken();
                exists = await _context.RefreshTokens
                    .AnyAsync(rt => rt.Token == refreshToken);
                retry++;

                if (retry > 10)
                {
                    _logger.LogError(
                        "Failed to generate unique refresh token after {Retry} attempts", retry);
                    throw new InvalidOperationException("Unable to generate a unique refresh token.");
                }
            } while (exists);

            var refreshTokenExpirationDays = int.Parse(
                _configuration["Jwt:RefreshTokenExpirationDays"] ?? "30");

            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = refreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            };

            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New refresh token generated for user {UserId}", user.Id);

            return new TokenResult
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = int.Parse(
                    _configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60") * 60,
                User = user
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<Guid?> ValidateAccessTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var secretKey = _configuration["Jwt:SecretKey"]
                ?? "DefaultSuperSecretKeyForSSOLoginService@1404!VeryLong";
            var key = Encoding.UTF8.GetBytes(secretKey);

            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "SSOLoginService",
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"] ?? "SSOLoginService.Client",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
                return Task.FromResult<Guid?>(userId);

            return Task.FromResult<Guid?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Token validation failed: {Message}", ex.Message);
            return Task.FromResult<Guid?>(null);
        }
    }

    public async Task<TokenResult?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var tokenEntity = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken && !rt.IsRevoked);

            if (tokenEntity == null || tokenEntity.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Invalid or expired refresh token");
                return null;
            }

            tokenEntity.IsRevoked = true;
            await _context.SaveChangesAsync();

            return await GenerateTokensAsync(tokenEntity.User);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return null;
        }
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        var tokenEntity = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (tokenEntity != null)
        {
            tokenEntity.IsRevoked = true;
            tokenEntity.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Refresh token revoked");
        }
    }

    public Guid? GetUserIdFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var secretKey = _configuration["Jwt:SecretKey"]
                ?? "DefaultSuperSecretKeyForSSOLoginService@1404!VeryLong";
            var key = Encoding.UTF8.GetBytes(secretKey);

            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
                return userId;

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error extracting user ID from token: {Message}", ex.Message);
            return null;
        }
    }
}
