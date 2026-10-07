using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Identity;

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> entity)
    {
        entity.ToTable("PasswordResetTokens");
        entity.HasKey(token => token.Id);
        entity.HasIndex(token => token.TokenHash).IsUnique();
        entity.HasIndex(token => new { token.UserId, token.ExpiresAtUtc });
        entity.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
        entity.Property(token => token.Purpose).HasConversion<string>().HasMaxLength(32).IsRequired();
        entity.HasIndex(token => new { token.UserId, token.Purpose, token.ExpiresAtUtc });
        entity.HasOne(token => token.User).WithMany(user => user.PasswordResetTokens).HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
