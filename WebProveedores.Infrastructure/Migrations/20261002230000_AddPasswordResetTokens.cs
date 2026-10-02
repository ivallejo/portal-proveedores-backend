using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebProveedores.Infrastructure.Migrations;

public partial class AddPasswordResetTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("PasswordResetTokenHash", "Users", type: "nvarchar(128)", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<DateTime>("PasswordResetTokenExpiresAtUtc", "Users", type: "datetime2", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("PasswordResetTokenHash", "Users");
        migrationBuilder.DropColumn("PasswordResetTokenExpiresAtUtc", "Users");
    }
}
