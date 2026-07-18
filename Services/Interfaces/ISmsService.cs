namespace SSOLoginService.Api.Services.Interfaces;

public interface ISmsService
{
    Task<bool> SendOtpAsync(string phoneNumber, string code);
}
