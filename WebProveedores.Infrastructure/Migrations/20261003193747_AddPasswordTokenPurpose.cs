using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebProveedores.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordTokenPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "PasswordResetTokens",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "PasswordReset");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId_Purpose_ExpiresAtUtc",
                table: "PasswordResetTokens",
                columns: new[] { "UserId", "Purpose", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PasswordResetTokens_UserId_Purpose_ExpiresAtUtc",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "PasswordResetTokens");
        }
    }
}
