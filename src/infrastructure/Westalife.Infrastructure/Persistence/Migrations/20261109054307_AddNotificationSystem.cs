using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotificationTestMode",
                table: "AppSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SmsHeader",
                table: "AppSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmsPassword",
                table: "AppSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmsProvider",
                table: "AppSettings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Netgsm");

            migrationBuilder.AddColumn<string>(
                name: "SmsUser",
                table: "AppSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpHost",
                table: "AppSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpPassword",
                table: "AppSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "AppSettings",
                type: "integer",
                nullable: false,
                defaultValue: 587);

            migrationBuilder.AddColumn<string>(
                name: "SmtpUser",
                table: "AppSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppPhoneNumberId",
                table: "AppSettings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppToken",
                table: "AppSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    RecipientAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationLogs_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000aa01"),
                columns: new[] { "SmsHeader", "SmsPassword", "SmsProvider", "SmsUser", "SmtpHost", "SmtpPassword", "SmtpPort", "SmtpUser", "WhatsAppPhoneNumberId", "WhatsAppToken" },
                values: new object[] { null, null, "Netgsm", null, null, null, 587, null, null, null });

            migrationBuilder.InsertData(
                table: "NotificationTemplates",
                columns: new[] { "Id", "Body", "Channel", "CreatedOn", "LastModifiedOn", "Name", "Subject" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111101"), "Sayın [AdSoyad], [DaireNo] numaralı dairenizin [Donem] dönemi bakiyesi [Tutar] tutarındadır. Westalife Residence yönetimi.", 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Aidat hatırlatma", "Aidat ve bakiye hatırlatması" },
                    { new Guid("11111111-1111-1111-1111-111111111102"), "Sayın [AdSoyad], [DaireNo] numaralı dairenizin [Donem] dönemi bakiyesi [Tutar] tutarındadır. Westalife Residence yönetimi.", 2, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Aidat hatırlatma", null },
                    { new Guid("11111111-1111-1111-1111-111111111103"), "Sayın [AdSoyad], [DaireNo] numaralı dairenizin [Donem] dönemi bakiyesi [Tutar] tutarındadır. Westalife Residence yönetimi.", 3, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Aidat hatırlatma", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_ApartmentId",
                table: "NotificationLogs",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_SentAt",
                table: "NotificationLogs",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Channel_Name",
                table: "NotificationTemplates",
                columns: new[] { "Channel", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "NotificationTestMode",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmsHeader",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmsPassword",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmsProvider",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmsUser",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmtpHost",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmtpPassword",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmtpUser",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "WhatsAppPhoneNumberId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "WhatsAppToken",
                table: "AppSettings");
        }
    }
}
