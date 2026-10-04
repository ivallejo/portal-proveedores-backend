using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebProveedores.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ruc = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ProviderRuc = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Igv = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Concept = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IssuedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegisteredById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisteredByName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RejectedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AreaName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApproverName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApproverEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalReferenceType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ApprovalReference = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    OrderType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    OrderNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OrderBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OrderDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Validation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Users_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Users_RegisteredById",
                        column: x => x.RegisteredById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentAttachments_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentEvents_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentItems_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Code",
                table: "Companies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAttachments_DocumentId",
                table: "DocumentAttachments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEvents_DocumentId_Sequence",
                table: "DocumentEvents",
                columns: new[] { "DocumentId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentItems_DocumentId",
                table: "DocumentItems",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ApproverId",
                table: "Documents",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_AreaId",
                table: "Documents",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CompanyId",
                table: "Documents",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProviderRuc_Number",
                table: "Documents",
                columns: new[] { "ProviderRuc", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_RegisteredById",
                table: "Documents",
                column: "RegisteredById");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Status_RegisteredAtUtc",
                table: "Documents",
                columns: new[] { "Status", "RegisteredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentAttachments");

            migrationBuilder.DropTable(
                name: "DocumentEvents");

            migrationBuilder.DropTable(
                name: "DocumentItems");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
