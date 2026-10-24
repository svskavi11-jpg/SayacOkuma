using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeterConnectionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MeterConnectionType",
                table: "AppSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TcpHost",
                table: "AppSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TcpPort",
                table: "AppSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UsbBaudRate",
                table: "AppSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UsbPortName",
                table: "AppSettings",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000aa01"),
                columns: new[] { "MeterConnectionType", "TcpHost", "TcpPort", "UsbBaudRate", "UsbPortName" },
                values: new object[] { "Simulator", null, 2000, 2400, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MeterConnectionType",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "TcpHost",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "TcpPort",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UsbBaudRate",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UsbPortName",
                table: "AppSettings");
        }
    }
}
