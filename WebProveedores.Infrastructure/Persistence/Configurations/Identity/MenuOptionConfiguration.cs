using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Access;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Identity;

internal sealed class MenuOptionConfiguration : IEntityTypeConfiguration<MenuOption>
{
    public void Configure(EntityTypeBuilder<MenuOption> entity)
    {
        entity.ToTable("MenuOptions");
        entity.HasKey(menu => menu.Id);
        entity.Property(menu => menu.Id).ValueGeneratedNever();
        entity.HasIndex(menu => menu.Code).IsUnique();
        entity.Property(menu => menu.Code).HasMaxLength(50).IsRequired();
        entity.Property(menu => menu.Name).HasMaxLength(80).IsRequired();
        entity.Property(menu => menu.Route).HasMaxLength(200);
        entity.Property(menu => menu.Icon).HasMaxLength(40).IsRequired();
        entity.Ignore(menu => menu.IsSystem);
        entity.HasOne(menu => menu.Parent).WithMany(menu => menu.Children).HasForeignKey(menu => menu.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

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
