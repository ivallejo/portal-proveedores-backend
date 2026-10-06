using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence.Configurations.Identity;

internal sealed class UserEmailConfiguration : IEntityTypeConfiguration<UserEmail>
{
    public void Configure(EntityTypeBuilder<UserEmail> entity)
    {
        entity.ToTable("UserEmails");
        entity.HasKey(email => email.Id);
        // La clave la genera el dominio: un correo agregado a un usuario existente debe insertarse.
        entity.Property(email => email.Id).ValueGeneratedNever();
        entity.HasIndex(email => email.Email).IsUnique();
        entity.HasIndex(email => new { email.UserId, email.IsPrimary }).HasFilter("[IsPrimary] = 1").IsUnique();
        entity.Property(email => email.Email).HasMaxLength(320).IsRequired();
        entity.Property(email => email.Type).HasDefaultValue(EmailType.Work);
        entity.Property(email => email.VerificationTokenHash).HasMaxLength(64);
        entity.HasIndex(email => email.VerificationTokenHash).IsUnique().HasFilter("[VerificationTokenHash] IS NOT NULL");
        entity.Ignore(email => email.IsVerified);
        entity.HasOne(email => email.User).WithMany(user => user.Emails).HasForeignKey(email => email.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
