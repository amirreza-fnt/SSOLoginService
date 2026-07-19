using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Json;
using SSOLoginService.Api.DTOs.MinistrySSO;
using SSOLoginService.Api.Services.Interfaces;

namespace SSOLoginService.Api.Services;

public class MoiSSOProvider : ISSOProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MoiSSOProvider> _logger;

    public SSOProviderType ProviderType => SSOProviderType.Moi;

    public bool IsActive => true;

    public MoiSSOProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MoiSSOProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<string> GetAuthorizationUrlAsync(string state, string? callbackUrl = null)
    {
        var config = GetMoiConfig();
        var redirectUri = callbackUrl ?? config.CallbackUrl;

        var query = new NameValueCollection
        {
            ["response_type"] = "code",
            ["scope"] = "openid profile",
            ["client_id"] = config.ClientId,
            ["state"] = state,
            ["redirect_uri"] = redirectUri,
            ["nonce"] = Guid.NewGuid().ToString("N")
        };

        var queryString = string.Join("&",
            query.AllKeys.Select(k => $"{WebUtility.UrlEncode(k)}={WebUtility.UrlEncode(query[k]!)}"));

        return Task.FromResult($"{config.AuthorizationUrl}?{queryString}");
    }

    public async Task<SSOCallbackResult> HandleCallbackAsync(IQueryCollection query)
    {
        try
        {
            var code = query["code"].FirstOrDefault();
            var state = query["state"].FirstOrDefault();
            var error = query["error"].FirstOrDefault();

            if (!string.IsNullOrEmpty(error))
                return new SSOCallbackResult { Error = $"خطا از سمت SSO: {error}" };

            if (string.IsNullOrWhiteSpace(code))
                return new SSOCallbackResult { Error = "کد احراز هویت دریافت نشد" };

            var config = GetMoiConfig();

            var tokenResponse = await ExchangeCodeForTokenAsync(code, config);
            if (tokenResponse == null)
                return new SSOCallbackResult { Error = "خطا در دریافت توکن" };

            var userInfo = await GetUserInfoAsync(tokenResponse.AccessToken, config);
            if (userInfo == null)
                return new SSOCallbackResult { Error = "خطا در دریافت اطلاعات کاربر" };

            if (string.IsNullOrWhiteSpace(userInfo.NationalId))
                return new SSOCallbackResult { Error = "کد ملی در اطلاعات کاربر یافت نشد" };

            return new SSOCallbackResult
            {
                UserInfo = new MinistrySSOUserInfo
                {
                    MelliCode = userInfo.NationalId,
                    FirstName = userInfo.FirstName,
                    LastName = userInfo.LastName,
                    FatherName = userInfo.FatherName,
                    BirthDate = userInfo.BirthDate,
                    Gender = userInfo.Gender,
                    Mobile = userInfo.Mobile,
                    Province = userInfo.Province,
                    City = userInfo.City,
                    PostalCode = userInfo.PostalCode,
                    Email = userInfo.Email,
                    PhoneList = userInfo.PhoneList
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MOI SSO callback error");
            return new SSOCallbackResult { Error = "خطای داخلی سرور" };
        }
    }

    public async Task LogoutAsync()
    {
        var config = GetMoiConfig();
        var client = _httpClientFactory.CreateClient("SSOClient");
        _ = await client.GetAsync(config.LogoutUrl);
    }

    private MoiConfig GetMoiConfig()
    {
        return new MoiConfig
        {
            AuthorizationUrl = _configuration["SSO:Moi:AuthorizationUrl"]
                ?? "https://ssokeshvar.moi.ir/oauth2/authorize",
            TokenUrl = _configuration["SSO:Moi:TokenUrl"]
                ?? "https://ssokeshvar.moi.ir/oauth2/token",
            UserInfoUrl = _configuration["SSO:Moi:UserInfoUrl"]
                ?? "https://ssokeshvar.moi.ir/api/v1/user/userinfo",
            JwksUrl = _configuration["SSO:Moi:JwksUrl"]
                ?? "http://ssokeshvar.moi.ir/oauth2/jwks",
            LogoutUrl = _configuration["SSO:Moi:LogoutUrl"]
                ?? "https://ssokeshvar.moi.ir/logout",
            ClientId = _configuration["SSO:Moi:ClientId"] ?? "",
            ClientSecret = _configuration["SSO:Moi:ClientSecret"] ?? "",
            CallbackUrl = _configuration["SSO:Moi:CallbackUrl"] ?? ""
        };
    }

    private async Task<MoiTokenResponse?> ExchangeCodeForTokenAsync(string code, MoiConfig config)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SSOClient");

            var basicAuth = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{config.ClientId}:{config.ClientSecret}"));

            var request = new HttpRequestMessage(HttpMethod.Post, config.TokenUrl);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicAuth);

            var formData = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "authorization_code"),
                new("code", code),
                new("redirect_uri", config.CallbackUrl),
                new("scope", "openid profile")
            };
            request.Content = new FormUrlEncodedContent(formData);

            _logger.LogInformation("Exchanging code for token at {Url}", config.TokenUrl);

            var response = await client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Token exchange failed: {Status} - {Body}",
                    response.StatusCode, json);
                return null;
            }

            var result = JsonSerializer.Deserialize<MoiTokenResponse>(json);
            if (result == null || string.IsNullOrWhiteSpace(result.AccessToken))
            {
                _logger.LogError("Token response missing access_token");
                return null;
            }

            _logger.LogInformation("Token received successfully, expires in {Seconds}s", result.ExpiresIn);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in token exchange");
            return null;
        }
    }

    private async Task<MoiUserInfoResponse?> GetUserInfoAsync(string accessToken, MoiConfig config)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SSOClient");

            var request = new HttpRequestMessage(HttpMethod.Get, config.UserInfoUrl);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            _logger.LogInformation("Getting user info from {Url}", config.UserInfoUrl);

            var response = await client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("User info request failed: {Status} - {Body}",
                    response.StatusCode, json);
                return null;
            }

            var userInfo = JsonSerializer.Deserialize<MoiUserInfoResponse>(json);

            _logger.LogInformation("User info received: nationalId={NationalId}",
                userInfo?.NationalId);

            return userInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in user info request");
            return null;
        }
    }

    private record MoiConfig
    {
        public string AuthorizationUrl { get; init; } = string.Empty;
        public string TokenUrl { get; init; } = string.Empty;
        public string UserInfoUrl { get; init; } = string.Empty;
        public string JwksUrl { get; init; } = string.Empty;
        public string LogoutUrl { get; init; } = string.Empty;
        public string ClientId { get; init; } = string.Empty;
        public string ClientSecret { get; init; } = string.Empty;
        public string CallbackUrl { get; init; } = string.Empty;
    }
}
