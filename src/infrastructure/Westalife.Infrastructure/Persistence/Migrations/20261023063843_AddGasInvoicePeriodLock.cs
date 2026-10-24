using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGasInvoicePeriodLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "GasInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedOn",
                table: "GasInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GasInvoices_IsLocked",
                table: "GasInvoices",
                column: "IsLocked");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GasInvoices_IsLocked",
                table: "GasInvoices");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "GasInvoices");

            migrationBuilder.DropColumn(
                name: "LockedOn",
                table: "GasInvoices");
        }
    }
}
