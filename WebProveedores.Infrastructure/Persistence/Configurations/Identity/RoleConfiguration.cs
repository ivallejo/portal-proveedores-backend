using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Identity;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> entity)
    {
        entity.ToTable("Roles");
        entity.HasKey(role => role.Id);
        entity.HasIndex(role => role.Code).IsUnique();
        entity.Property(role => role.Code).HasMaxLength(50).IsRequired();
        entity.Property(role => role.Name).HasMaxLength(120).IsRequired();
        entity.Property(role => role.Description).HasMaxLength(300);
        entity.Ignore(role => role.IsSystem);
    }
}
