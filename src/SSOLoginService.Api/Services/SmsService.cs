using SSOLoginService.Api.Services.Interfaces;

namespace SSOLoginService.Api.Services;

public class SmsService : ISmsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmsService> _logger;

    public SmsService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SmsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendOtpAsync(string phoneNumber, string code)
    {
        try
        {
            var provider = _configuration["Sms:Provider"] ?? "Console";

            switch (provider.ToLower())
            {
                case "kavenegar":
                    return await SendViaKavenegarAsync(phoneNumber, code);
                case "farazsms":
                    return await SendViaFarazSmsAsync(phoneNumber, code);
                case "console":
                default:
                    _logger.LogInformation("[SMS] To: {Phone}, Code: {Code}", phoneNumber, code);
                    return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", phoneNumber);
            return false;
        }
    }

    private async Task<bool> SendViaKavenegarAsync(string phoneNumber, string code)
    {
        var apiKey = _configuration["Sms:Kavenegar:ApiKey"];
        var template = _configuration["Sms:Kavenegar:Template"] ?? "otp";

        var client = _httpClientFactory.CreateClient("SmsClient");
        var url = $"https://api.kavenegar.com/v1/{apiKey}/verify/lookup.json" +
                  $"?receptor={phoneNumber}&token={code}&template={template}";

        var response = await client.GetAsync(url);
        return response.IsSuccessStatusCode;
    }

    private async Task<bool> SendViaFarazSmsAsync(string phoneNumber, string code)
    {
        var apiKey = _configuration["Sms:FarazSms:ApiKey"];
        var from = _configuration["Sms:FarazSms:From"];

        var client = _httpClientFactory.CreateClient("SmsClient");
        var payload = new
        {
            from,
            to = phoneNumber,
            text = $"کد تایید شما: {code}\nاعتبار: ۲ دقیقه"
        };

        var response = await client.PostAsJsonAsync(
            $"https://api.sms.ir/v1/send", payload);
        return response.IsSuccessStatusCode;
    }
}
