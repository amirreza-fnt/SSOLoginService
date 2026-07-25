namespace SSOLoginService.Api.Services.Interfaces;

public interface IOtpService
{
    Task<string> GenerateAndSendOtpAsync(string phoneNumber, string? melliCode = null);
    Task<bool> VerifyOtpAsync(string phoneNumber, string code, string? melliCode = null);
    Task CleanExpiredOtpsAsync();
}
