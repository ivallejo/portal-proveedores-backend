using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebProveedores.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyBillingEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingEmail",
                table: "Companies",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingEmail",
                table: "Companies");
        }
    }
}
