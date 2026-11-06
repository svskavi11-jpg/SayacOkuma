using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagementPeriodToAppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ManagementPeriodEnd",
                table: "AppSettings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ManagementPeriodStart",
                table: "AppSettings",
                type: "date",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000aa01"),
                columns: new[] { "ManagementPeriodEnd", "ManagementPeriodStart" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagementPeriodEnd",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ManagementPeriodStart",
                table: "AppSettings");
        }
    }
}
