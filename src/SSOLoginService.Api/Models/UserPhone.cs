using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SSOLoginService.Api.Models;

[Table("UserPhones")]
public class UserPhone
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(15)]
    public string PhoneNumber { get; set; } = string.Empty;

    public bool IsVerified { get; set; } = true;

    public bool IsPrimary { get; set; } = false;

    [MaxLength(100)]
    public string? Source { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }
}
