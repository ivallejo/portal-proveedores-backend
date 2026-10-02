using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebProveedores.Infrastructure.Migrations;

public partial class AddUsernameAndArea : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Area",
            table: "Users",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Username",
            table: "Users",
            type: "nvarchar(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.Sql("UPDATE [Users] SET [Username] = CASE WHEN [Ruc] = 'ADMIN-SYSTEM' THEN 'admin' ELSE [Ruc] END WHERE [Username] IS NULL AND [Ruc] IS NOT NULL AND LTRIM(RTRIM([Ruc])) <> ''");

        migrationBuilder.CreateIndex(
            name: "IX_Users_Username",
            table: "Users",
            column: "Username",
            unique: true,
            filter: "[Username] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Users_Username", table: "Users");
        migrationBuilder.DropColumn(name: "Area", table: "Users");
        migrationBuilder.DropColumn(name: "Username", table: "Users");
    }
}
