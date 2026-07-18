using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SSOLoginService.Api.DTOs.MinistrySSO;
using SSOLoginService.Api.Services.Interfaces;

namespace SSOLoginService.Api.Services;

public class MinistrySSOService : IMinistrySSOService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MinistrySSOService> _logger;

    public MinistrySSOService(
        IConfiguration configuration,
        ILogger<MinistrySSOService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<string> GetLoginUrlAsync(string state)
    {
        var appId = _configuration["MinistrySSO:AppId"];
        var baseUrl = _configuration["MinistrySSO:BaseUrl"] ?? "https://sso.moi.ir";
        var loginUrl = $"{baseUrl}/login?appid={appId}&state={state}";
        return Task.FromResult(loginUrl);
    }

    public Task<bool> ValidateStateAsync(string state)
    {
        return Task.FromResult(!string.IsNullOrEmpty(state) && state.Length >= 10);
    }

    public Task<MinistrySSOUserInfo?> DecryptAndExtractUserInfoAsync(
        string encryptedData, byte[] ivKey, string appSecret)
    {
        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedData);

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.KeySize = 256;
            aes.BlockSize = 128;

            using var rfc = new Rfc2898DeriveBytes(appSecret, ivKey, 1000, HashAlgorithmName.SHA1);
            aes.Key = rfc.GetBytes(32);
            aes.IV = rfc.GetBytes(16);

            using var ms = new MemoryStream(encryptedBytes);
            using var decrypt = aes.CreateDecryptor();
            using var cs = new CryptoStream(ms, decrypt, CryptoStreamMode.Read);
            using var output = new MemoryStream();
            cs.CopyTo(output);

            var decryptedData = Encoding.UTF8.GetString(output.ToArray());
            _logger.LogInformation("SSO decrypted data: {Data}", decryptedData);

            var userInfo = JsonSerializer.Deserialize<MinistrySSOUserInfo>(decryptedData);
            return Task.FromResult(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt SSO user data");
            return Task.FromResult<MinistrySSOUserInfo?>(null);
        }
    }
}
