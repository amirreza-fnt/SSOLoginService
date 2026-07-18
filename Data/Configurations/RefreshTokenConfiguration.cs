using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSOLoginService.Api.Models;

namespace SSOLoginService.Api.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasIndex(rt => rt.Token).IsUnique();
        builder.HasQueryFilter(rt => !rt.IsRevoked);

        builder.Property(rt => rt.Token).IsRequired();
    }
}
