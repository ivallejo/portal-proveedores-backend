using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Organization;

internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> entity)
    {
        entity.ToTable("Areas");
        entity.HasKey(area => area.Id);
        // El nombre (y su clave) es único en todo el portal: una misma área puede estar en varias sociedades.
        entity.HasIndex(area => area.Code).IsUnique();
        entity.Property(area => area.Code).HasMaxLength(50).IsRequired();
        entity.Property(area => area.Name).HasMaxLength(120).IsRequired();
        entity.Property(area => area.Description).HasMaxLength(300);
        entity.Ignore(area => area.CompanyCodes);
    }
}
