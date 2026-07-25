using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSOLoginService.Api.Models;

namespace SSOLoginService.Api.Data.Configurations;

public class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.HasIndex(o => new { o.PhoneNumber, o.IsUsed });
        builder.HasIndex(o => o.ExpiresAt);

        builder.Property(o => o.Code).IsRequired();
        builder.Property(o => o.PhoneNumber).IsRequired();
    }
}
