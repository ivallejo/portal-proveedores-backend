using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Identity;

internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> entity)
    {
        entity.ToTable("Areas");
        entity.HasKey(area => area.Id);
        // El nombre (y su clave) es único dentro de cada sociedad; dos sociedades pueden tener «Finanzas».
        entity.HasIndex(area => new { area.CompanyId, area.Code }).IsUnique();
        entity.Property(area => area.Code).HasMaxLength(50).IsRequired();
        entity.Property(area => area.Name).HasMaxLength(120).IsRequired();
        entity.Property(area => area.Description).HasMaxLength(300);
        entity.HasOne(area => area.Company).WithMany().HasForeignKey(area => area.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
