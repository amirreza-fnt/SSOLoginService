using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SSOLoginService.Api.Models;

[Table("Partners")]
public class Partner
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ApiKey { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? AllowedIps { get; set; }

    [MaxLength(1000)]
    public string? AllowedRedirectUris { get; set; }

    public bool IsActive { get; set; } = true;

    public int RateLimitPerMinute { get; set; } = 60;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
