using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApartmentInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GasInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SquareMetersUsed = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    ConsumptionKWhUsed = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    FixedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ConsumptionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsMeterReadFailed = table.Column<bool>(type: "boolean", nullable: false),
                    CalculatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsSuperseded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApartmentInvoices_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApartmentInvoices_GasInvoices_GasInvoiceId",
                        column: x => x.GasInvoiceId,
                        principalTable: "GasInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoices_ApartmentId",
                table: "ApartmentInvoices",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoices_GasInvoiceId",
                table: "ApartmentInvoices",
                column: "GasInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoices_IsSuperseded",
                table: "ApartmentInvoices",
                column: "IsSuperseded");

            migrationBuilder.CreateIndex(
                name: "UX_ApartmentInvoices_GasInvoice_Apartment_Active",
                table: "ApartmentInvoices",
                columns: new[] { "GasInvoiceId", "ApartmentId" },
                unique: true,
                filter: "\"IsSuperseded\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApartmentInvoices");
        }
    }
}
