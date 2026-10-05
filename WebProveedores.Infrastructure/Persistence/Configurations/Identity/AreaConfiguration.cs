using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Identity;

internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> entity)
    {
        entity.ToTable("Areas");
        entity.HasKey(area => area.Id);
        entity.HasIndex(area => area.Code).IsUnique();
        entity.Property(area => area.Code).HasMaxLength(50).IsRequired();
        entity.Property(area => area.Name).HasMaxLength(120).IsRequired();
    }
}
