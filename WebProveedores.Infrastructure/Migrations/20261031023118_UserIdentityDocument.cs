using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebProveedores.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserIdentityDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El DNI pasa a ser un documento con tipo: se renombra la columna (sin perder datos) y se marca DNI en los existentes.
            migrationBuilder.DropIndex(
                name: "IX_Users_Dni",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "Dni",
                table: "Users",
                newName: "DocumentNumber");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentNumber",
                table: "Users",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql("UPDATE [Users] SET [DocumentType] = N'Dni' WHERE [DocumentNumber] IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DocumentType_DocumentNumber",
                table: "Users",
                columns: new[] { "DocumentType", "DocumentNumber" },
                unique: true,
                filter: "[DocumentNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_DocumentType_DocumentNumber",
                table: "Users");

            // Solo los DNI vuelven a la columna anterior.
            migrationBuilder.Sql("UPDATE [Users] SET [DocumentNumber] = NULL WHERE [DocumentType] <> N'Dni';");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentNumber",
                table: "Users",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "DocumentNumber",
                table: "Users",
                newName: "Dni");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Dni",
                table: "Users",
                column: "Dni",
                unique: true,
                filter: "[Dni] IS NOT NULL");
        }
    }
}
