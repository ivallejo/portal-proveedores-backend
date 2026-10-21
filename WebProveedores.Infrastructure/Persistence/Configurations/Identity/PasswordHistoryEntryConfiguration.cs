using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Identity;

internal sealed class PasswordHistoryEntryConfiguration : IEntityTypeConfiguration<PasswordHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PasswordHistoryEntry> entity)
    {
        entity.ToTable("PasswordHistory");
        entity.HasKey(entry => entry.Id);
        entity.Property(entry => entry.Id).ValueGeneratedNever();
        entity.Property(entry => entry.PasswordHash).HasMaxLength(500).IsRequired();
        entity.HasIndex(entry => new { entry.UserId, entry.SetAtUtc });
        entity.HasOne<AppUser>().WithMany(user => user.PasswordHistory).HasForeignKey(entry => entry.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
