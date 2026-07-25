using SSOLoginService.Api.DTOs.MinistrySSO;

namespace SSOLoginService.Api.Services.Interfaces;

public interface IMinistrySSOService
{
    Task<string> GetLoginUrlAsync(string state);
    Task<bool> ValidateStateAsync(string state);
    Task<MinistrySSOUserInfo?> DecryptAndExtractUserInfoAsync(
        string encryptedData, byte[] ivKey, string appSecret);
}
