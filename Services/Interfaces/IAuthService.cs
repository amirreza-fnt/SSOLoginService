using SSOLoginService.Api.DTOs.Auth;
using SSOLoginService.Api.DTOs.MinistrySSO;

namespace SSOLoginService.Api.Services.Interfaces;

public interface IAuthService
{
    Task<UserTokenResponse> ProcessMinistrySSOLoginAsync(MinistrySSOUserInfo ssoUserInfo);
    Task<UserTokenResponse> ProcessOtpLoginAsync(string phoneNumber, string melliCode);
    Task<List<UserPhoneDto>> GetUserPhonesAsync(string melliCode);
    Task<UserInfoDto?> GetCurrentUserAsync(Guid userId);
    Task LogoutAsync(Guid userId);
}
