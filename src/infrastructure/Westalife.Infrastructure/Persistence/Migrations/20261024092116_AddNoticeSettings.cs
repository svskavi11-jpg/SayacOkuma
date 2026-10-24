using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNoticeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultNoticeNote",
                table: "AppSettings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultSubscriberNumber",
                table: "AppSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DueDateDays",
                table: "AppSettings",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<byte[]>(
                name: "LogoLeft",
                table: "AppSettings",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoLeftContentType",
                table: "AppSettings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "LogoRight",
                table: "AppSettings",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoRightContentType",
                table: "AppSettings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoticeLeftTitle",
                table: "AppSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoticeRightTitle",
                table: "AppSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReadingServiceFee",
                table: "AppSettings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000aa01"),
                columns: new[] { "DefaultNoticeNote", "DefaultSubscriberNumber", "DueDateDays", "LogoLeft", "LogoLeftContentType", "LogoRight", "LogoRightContentType", "NoticeLeftTitle", "NoticeRightTitle" },
                values: new object[] { null, null, 15, null, null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultNoticeNote",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultSubscriberNumber",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DueDateDays",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LogoLeft",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LogoLeftContentType",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LogoRight",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LogoRightContentType",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NoticeLeftTitle",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "NoticeRightTitle",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ReadingServiceFee",
                table: "AppSettings");
        }
    }
}
