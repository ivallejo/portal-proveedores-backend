using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Access;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Access;

internal sealed class RoleMenuConfiguration : IEntityTypeConfiguration<RoleMenu>
{
    public void Configure(EntityTypeBuilder<RoleMenu> entity)
    {
        entity.ToTable("RoleMenus");
        entity.HasKey(item => new { item.RoleId, item.MenuOptionId });
        entity.HasOne(item => item.Role).WithMany(role => role.RoleMenus).HasForeignKey(item => item.RoleId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.MenuOption).WithMany(menu => menu.RoleMenus).HasForeignKey(item => item.MenuOptionId).OnDelete(DeleteBehavior.Cascade);
    }
}
