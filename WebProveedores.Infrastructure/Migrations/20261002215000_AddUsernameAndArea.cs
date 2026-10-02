using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebProveedores.Infrastructure.Migrations;

public partial class AddUsernameAndArea : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Area", "Users", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>("Username", "Users", type: "nvarchar(80)", maxLength: 80, nullable: true);
        migrationBuilder.Sql("UPDATE [Users] SET [Username] = CASE WHEN [Ruc] = 'ADMIN-SYSTEM' THEN 'admin' ELSE [Ruc] END WHERE [Username] IS NULL AND [Ruc] IS NOT NULL AND LTRIM(RTRIM([Ruc])) <> ''");
        migrationBuilder.CreateIndex("IX_Users_Username", "Users", "Username", unique: true, filter: "[Username] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Users_Username", "Users");
        migrationBuilder.DropColumn("Area", "Users");
        migrationBuilder.DropColumn("Username", "Users");
    }
}
