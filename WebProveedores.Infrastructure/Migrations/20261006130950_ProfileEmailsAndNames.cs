using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebProveedores.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProfileEmailsAndNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "UserEmails",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationExpiresAtUtc",
                table: "UserEmails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationTokenHash",
                table: "UserEmails",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAtUtc",
                table: "UserEmails",
                type: "datetime2",
                nullable: true);

            // Los correos existentes los registró el administrador, el seed o SAP: se dan por verificados.
            migrationBuilder.Sql("UPDATE [UserEmails] SET [VerifiedAtUtc] = [CreatedAtUtc] WHERE [VerifiedAtUtc] IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_UserEmails_VerificationTokenHash",
                table: "UserEmails",
                column: "VerificationTokenHash",
                unique: true,
                filter: "[VerificationTokenHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserEmails_VerificationTokenHash",
                table: "UserEmails");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "UserEmails");

            migrationBuilder.DropColumn(
                name: "VerificationExpiresAtUtc",
                table: "UserEmails");

            migrationBuilder.DropColumn(
                name: "VerificationTokenHash",
                table: "UserEmails");

            migrationBuilder.DropColumn(
                name: "VerifiedAtUtc",
                table: "UserEmails");
        }
    }
}
