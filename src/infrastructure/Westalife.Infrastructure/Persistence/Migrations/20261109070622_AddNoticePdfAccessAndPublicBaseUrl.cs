using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNoticePdfAccessAndPublicBaseUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicBaseUrl",
                table: "AppSettings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NoticePdfAccessTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<Guid>(type: "uuid", nullable: false),
                    GasInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticePdfAccessTokens", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000aa01"),
                column: "PublicBaseUrl",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_NoticePdfAccessTokens_Invoice_Apartment",
                table: "NoticePdfAccessTokens",
                columns: new[] { "GasInvoiceId", "ApartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_NoticePdfAccessTokens_Token",
                table: "NoticePdfAccessTokens",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoticePdfAccessTokens");

            migrationBuilder.DropColumn(
                name: "PublicBaseUrl",
                table: "AppSettings");
        }
    }
}
