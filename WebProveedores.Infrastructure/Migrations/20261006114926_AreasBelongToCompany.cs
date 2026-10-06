using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebProveedores.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AreasBelongToCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Areas_Code",
                table: "Areas");

            // Las áreas existentes pasan a la sociedad 1001 (Naviera Transoceánica); luego la columna es obligatoria.
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Areas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE Areas SET CompanyId = (SELECT TOP 1 Id FROM Companies WHERE Code = '1001') WHERE CompanyId IS NULL;" +
                "IF EXISTS (SELECT 1 FROM Areas WHERE CompanyId IS NULL) " +
                "  UPDATE Areas SET CompanyId = (SELECT TOP 1 Id FROM Companies ORDER BY Code) WHERE CompanyId IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "Areas",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Areas",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Areas_CompanyId_Code",
                table: "Areas",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Areas_Companies_CompanyId",
                table: "Areas",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Areas_Companies_CompanyId",
                table: "Areas");

            migrationBuilder.DropIndex(
                name: "IX_Areas_CompanyId_Code",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Areas");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_Code",
                table: "Areas",
                column: "Code",
                unique: true);
        }
    }
}
