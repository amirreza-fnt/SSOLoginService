using System.ComponentModel.DataAnnotations;

namespace SSOLoginService.Api.DTOs.Auth;

public class SecondLoginVerifyRequest
{
    [Required]
    [MaxLength(15)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(6)]
    public string OtpCode { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? MelliCode { get; set; }
}
