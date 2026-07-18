using System.ComponentModel.DataAnnotations;

namespace SSOLoginService.Api.DTOs.Auth;

public class SecondLoginRequest
{
    [Required]
    [MaxLength(20)]
    public string MelliCode { get; set; } = string.Empty;
}
