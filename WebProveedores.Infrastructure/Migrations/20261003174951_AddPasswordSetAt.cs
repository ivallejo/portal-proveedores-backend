using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebProveedores.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordSetAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordSetAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PasswordSetAtUtc",
                table: "Users",
                column: "PasswordSetAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_PasswordSetAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordSetAtUtc",
                table: "Users");
        }
    }
}
