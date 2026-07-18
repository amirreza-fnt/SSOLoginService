namespace SSOLoginService.Api.DTOs.Auth;

public class UserPhoneDto
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
