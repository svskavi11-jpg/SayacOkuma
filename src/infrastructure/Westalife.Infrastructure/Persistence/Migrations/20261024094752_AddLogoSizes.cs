using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLogoSizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LogoLeftHeightMm",
                table: "AppSettings",
                type: "integer",
                nullable: false,
                defaultValue: 17);

            migrationBuilder.AddColumn<int>(
                name: "LogoRightHeightMm",
                table: "AppSettings",
                type: "integer",
                nullable: false,
                defaultValue: 17);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000aa01"),
                columns: new[] { "LogoLeftHeightMm", "LogoRightHeightMm" },
                values: new object[] { 17, 17 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoLeftHeightMm",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LogoRightHeightMm",
                table: "AppSettings");
        }
    }
}
