using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebProveedores.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryType",
                table: "Documents",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            // Hasta hoy las órdenes de servicio se validaban como entregadas a bordo.
            migrationBuilder.Sql("UPDATE [Documents] SET [DeliveryType] = N'OnBoard' WHERE [OrderType] = N'Service';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryType",
                table: "Documents");
        }
    }
}
