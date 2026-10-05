using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Identity;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> entity)
    {
        entity.ToTable("Users");
        entity.HasKey(user => user.Id);
        entity.HasIndex(user => user.Username).IsUnique();
        entity.HasIndex(user => user.Ruc).IsUnique().HasFilter("[Ruc] IS NOT NULL");
        entity.HasIndex(user => user.AreaId);
        entity.Property(user => user.Username).HasMaxLength(80).IsRequired();
        entity.Property(user => user.CompanyName).HasMaxLength(200).IsRequired();
        entity.Property(user => user.Ruc).HasMaxLength(20);
        entity.Property(user => user.PasswordHash).HasMaxLength(500).IsRequired();
        entity.HasIndex(user => user.PasswordSetAtUtc);
        entity.Property(user => user.FailedLoginCount).HasDefaultValue(0);
        entity.Property(user => user.MustChangePassword).HasDefaultValue(false);
        entity.HasOne(user => user.Area).WithMany(area => area.Users).HasForeignKey(user => user.AreaId).OnDelete(DeleteBehavior.Restrict);
    }
}
