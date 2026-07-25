using Microsoft.EntityFrameworkCore;
using SSOLoginService.Api.Data;
using SSOLoginService.Api.DTOs.Auth;
using SSOLoginService.Api.DTOs.MinistrySSO;
using SSOLoginService.Api.Models;
using SSOLoginService.Api.Services.Interfaces;

namespace SSOLoginService.Api.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext context,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<UserTokenResponse> ProcessMinistrySSOLoginAsync(MinistrySSOUserInfo ssoUserInfo)
    {
        _logger.LogInformation("Processing Ministry SSO login for MelliCode: {MelliCode}", ssoUserInfo.MelliCode);

        var user = await FindOrCreateUserFromSSOAsync(ssoUserInfo);
        var tokens = await _tokenService.GenerateTokensAsync(user);

        _logger.LogInformation("User {UserId} logged in via Ministry SSO", user.Id);

        return BuildTokenResponse(tokens, user);
    }

    public async Task<UserTokenResponse> ProcessOtpLoginAsync(string phoneNumber, string melliCode)
    {
        _logger.LogInformation("Processing OTP login for phone: {Phone}, melliCode: {MelliCode}", phoneNumber, melliCode);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.MelliCode == melliCode);

        if (user == null)
            throw new InvalidOperationException("کاربر با این کد ملی یافت نشد");

        user.LastLoginAt = DateTime.UtcNow;

        var tokens = await _tokenService.GenerateTokensAsync(user);

        _logger.LogInformation("User {UserId} logged in via OTP", user.Id);

        return BuildTokenResponse(tokens, user);
    }

    public async Task<List<UserPhoneDto>> GetUserPhonesAsync(string melliCode)
    {
        var phones = await _context.UserPhones
            .Where(p => p.User != null && p.User.MelliCode == melliCode && p.IsVerified)
            .Select(p => new UserPhoneDto
            {
                Id = p.Id,
                PhoneNumber = p.PhoneNumber,
                IsPrimary = p.IsPrimary
            })
            .ToListAsync();

        return phones;
    }

    public async Task<UserInfoDto?> GetCurrentUserAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.Phones)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return null;

        return new UserInfoDto
        {
            Id = user.Id.ToString(),
            MelliCode = user.MelliCode,
            Phone = user.Phones.FirstOrDefault(p => p.IsPrimary)?.PhoneNumber
                     ?? user.Phones.FirstOrDefault()?.PhoneNumber
        };
    }

    public async Task LogoutAsync(Guid userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.RevokeReason = "User logged out";
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("User {UserId} logged out", userId);
    }

    private async Task<User> FindOrCreateUserFromSSOAsync(MinistrySSOUserInfo ssoUserInfo)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.MelliCode == ssoUserInfo.MelliCode);

        if (existingUser != null)
        {
            existingUser.LastLoginAt = DateTime.UtcNow;
            await SyncPhoneAsync(existingUser, ssoUserInfo.Mobile);
            await _context.SaveChangesAsync();
            return existingUser;
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            MelliCode = ssoUserInfo.MelliCode ?? throw new InvalidOperationException("MelliCode is required"),
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Users.Add(newUser);

        if (!string.IsNullOrWhiteSpace(ssoUserInfo.Mobile))
        {
            newUser.Phones.Add(new UserPhone
            {
                PhoneNumber = ssoUserInfo.Mobile,
                IsVerified = true,
                IsPrimary = true,
                Source = "MinistrySSO"
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("New user created from Ministry SSO: {UserId}", newUser.Id);
        return newUser;
    }

    private async Task SyncPhoneAsync(User user, string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return;

        var phoneExists = await _context.UserPhones
            .AnyAsync(p => p.UserId == user.Id && p.PhoneNumber == mobile);

        if (!phoneExists)
        {
            _context.UserPhones.Add(new UserPhone
            {
                UserId = user.Id,
                PhoneNumber = mobile,
                IsVerified = true,
                IsPrimary = !await _context.UserPhones.AnyAsync(p => p.UserId == user.Id),
                Source = "MinistrySSO"
            });
        }
    }

    private static UserTokenResponse BuildTokenResponse(TokenResult tokens, User user)
    {
        return new UserTokenResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresIn = tokens.ExpiresIn,
            TokenType = "Bearer",
            User = new UserInfoDto
            {
                Id = user.Id.ToString(),
                MelliCode = user.MelliCode
            }
        };
    }
}
