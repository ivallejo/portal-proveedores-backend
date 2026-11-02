using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebProveedores.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AreaCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Un área puede estar en varias sociedades: primero se crea la relación con la sociedad actual de cada área.
            migrationBuilder.CreateTable(
                name: "AreaCompanies",
                columns: table => new
                {
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreaCompanies", x => new { x.AreaId, x.CompanyId });
                    table.ForeignKey(
                        name: "FK_AreaCompanies_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AreaCompanies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // El nombre pasa a ser único: las áreas con el mismo nombre en varias sociedades se unen en la más antigua,
            // con sus sociedades, usuarios y documentos. Todo en un solo bloque (la tabla temporal vive en la sesión).
            migrationBuilder.Sql("""
                INSERT INTO [AreaCompanies] ([AreaId], [CompanyId]) SELECT [Id], [CompanyId] FROM [Areas];

                SELECT [d].[Id] AS [OldId], [s].[Id] AS [NewId]
                INTO #AreaMerge
                FROM (SELECT [Id], [Code], ROW_NUMBER() OVER (PARTITION BY [Code] ORDER BY [CreatedAtUtc], [Id]) AS [N] FROM [Areas]) AS [d]
                JOIN (SELECT [Id], [Code], ROW_NUMBER() OVER (PARTITION BY [Code] ORDER BY [CreatedAtUtc], [Id]) AS [N] FROM [Areas]) AS [s]
                    ON [s].[Code] = [d].[Code] AND [s].[N] = 1
                WHERE [d].[N] > 1;

                UPDATE [u] SET [AreaId] = [m].[NewId] FROM [Users] AS [u] JOIN #AreaMerge AS [m] ON [u].[AreaId] = [m].[OldId];
                UPDATE [doc] SET [AreaId] = [m].[NewId] FROM [Documents] AS [doc] JOIN #AreaMerge AS [m] ON [doc].[AreaId] = [m].[OldId];
                INSERT INTO [AreaCompanies] ([AreaId], [CompanyId])
                    SELECT DISTINCT [m].[NewId], [ac].[CompanyId]
                    FROM [AreaCompanies] AS [ac] JOIN #AreaMerge AS [m] ON [ac].[AreaId] = [m].[OldId]
                    WHERE NOT EXISTS (SELECT 1 FROM [AreaCompanies] AS [x] WHERE [x].[AreaId] = [m].[NewId] AND [x].[CompanyId] = [ac].[CompanyId]);
                -- Si alguna de las unidas estaba activa, la que queda también.
                UPDATE [a] SET [IsActive] = CAST(1 AS bit) FROM [Areas] AS [a]
                    WHERE EXISTS (SELECT 1 FROM #AreaMerge AS [m] JOIN [Areas] AS [o] ON [o].[Id] = [m].[OldId] WHERE [m].[NewId] = [a].[Id] AND [o].[IsActive] = CAST(1 AS bit));
                DELETE [a] FROM [Areas] AS [a] JOIN #AreaMerge AS [m] ON [a].[Id] = [m].[OldId];
                DROP TABLE #AreaMerge;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Areas_Companies_CompanyId",
                table: "Areas");

            migrationBuilder.DropIndex(
                name: "IX_Areas_CompanyId_Code",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Areas");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_Code",
                table: "Areas",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AreaCompanies_CompanyId",
                table: "AreaCompanies",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Vuelve a una sociedad por área (la de menor código); las áreas unidas no se separan.
            migrationBuilder.DropIndex(
                name: "IX_Areas_Code",
                table: "Areas");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Areas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [a] SET [CompanyId] = (
                    SELECT TOP (1) [ac].[CompanyId] FROM [AreaCompanies] AS [ac] JOIN [Companies] AS [c] ON [c].[Id] = [ac].[CompanyId]
                    WHERE [ac].[AreaId] = [a].[Id] ORDER BY [c].[Code])
                FROM [Areas] AS [a];
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: "Areas",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "AreaCompanies");

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
    }
}
