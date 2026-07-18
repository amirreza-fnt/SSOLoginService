using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSOLoginService.Api.Models;

namespace SSOLoginService.Api.Data.Configurations;

public class UserPhoneConfiguration : IEntityTypeConfiguration<UserPhone>
{
    public void Configure(EntityTypeBuilder<UserPhone> builder)
    {
        builder.HasIndex(p => p.PhoneNumber);
        builder.HasIndex(p => new { p.UserId, p.PhoneNumber }).IsUnique();

        builder.Property(p => p.PhoneNumber).IsRequired();
    }
}
