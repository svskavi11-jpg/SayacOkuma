using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDocumentNumberColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DocumentNumber_Active",
                table: "Payments",
                column: "DocumentNumber",
                filter: "\"IsCancelled\" = false AND \"DocumentNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_DocumentNumber_Active",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                table: "Payments");
        }
    }
}
