using Microsoft.EntityFrameworkCore;
using SSOLoginService.Api.Data;
using SSOLoginService.Api.Models;
using SSOLoginService.Api.Services.Interfaces;
using System.Security.Cryptography;

namespace SSOLoginService.Api.Services;

public class OtpService : IOtpService
{
    private readonly AppDbContext _context;
    private readonly ISmsService _smsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        AppDbContext context,
        ISmsService smsService,
        IConfiguration configuration,
        ILogger<OtpService> logger)
    {
        _context = context;
        _smsService = smsService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GenerateAndSendOtpAsync(string phoneNumber, string? melliCode = null)
    {
        var otpLength = int.Parse(_configuration["Otp:Length"] ?? "5");
        var otpExpirationMinutes = int.Parse(_configuration["Otp:ExpirationMinutes"] ?? "2");
        var maxAttempts = int.Parse(_configuration["Otp:MaxAttempts"] ?? "5");

        var recentCodes = await _context.OtpCodes
            .Where(o => o.PhoneNumber == phoneNumber
                     && !o.IsUsed
                     && o.CreatedAt > DateTime.UtcNow.AddMinutes(-5))
            .CountAsync();

        if (recentCodes >= 3)
            throw new InvalidOperationException("تعداد درخواست‌های کد تایید بیش از حد مجاز است. لطفاً بعداً تلاش کنید.");

        var code = GenerateNumericCode(otpLength);

        var otpCode = new OtpCode
        {
            PhoneNumber = phoneNumber,
            Code = code,
            MelliCode = melliCode,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(otpExpirationMinutes),
            IsUsed = false,
            AttemptCount = 0
        };

        _context.OtpCodes.Add(otpCode);

        await _context.SaveChangesAsync();

        var sent = await _smsService.SendOtpAsync(phoneNumber, code);
        if (!sent)
            _logger.LogWarning("Failed to send OTP to {PhoneNumber}", phoneNumber);

        _logger.LogInformation("OTP sent to {PhoneNumber}", phoneNumber);

        return code;
    }

    public async Task<bool> VerifyOtpAsync(string phoneNumber, string code, string? melliCode = null)
    {
        var otpCode = await _context.OtpCodes
            .Where(o => o.PhoneNumber == phoneNumber
                     && o.Code == code
                     && !o.IsUsed
                     && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpCode == null)
            return false;

        if (otpCode.AttemptCount >= 5)
        {
            otpCode.IsUsed = true;
            await _context.SaveChangesAsync();
            return false;
        }

        otpCode.IsUsed = true;
        otpCode.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task CleanExpiredOtpsAsync()
    {
        var expired = await _context.OtpCodes
            .Where(o => o.ExpiresAt < DateTime.UtcNow || o.IsUsed)
            .Take(1000)
            .ToListAsync();

        if (expired.Count != 0)
        {
            _context.OtpCodes.RemoveRange(expired);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Cleaned {Count} expired OTP codes", expired.Count);
        }
    }

    private static string GenerateNumericCode(int length)
    {
        var randomBytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var code = new char[length];
        for (var i = 0; i < length; i++)
        {
            code[i] = (char)('0' + (randomBytes[i] % 10));
        }

        return new string(code);
    }
}
