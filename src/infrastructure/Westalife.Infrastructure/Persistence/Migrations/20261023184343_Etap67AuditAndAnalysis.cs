using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Etap67AuditAndAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "GasInvoices",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApartmentOwners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentOwners", x => x.Id);
                    table.CheckConstraint("CK_ApartmentOwners_Period", "\"ValidFrom\" < COALESCE(\"ValidTo\", 'infinity'::timestamptz)");
                    table.ForeignKey(
                        name: "FK_ApartmentOwners_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeterReadingAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeterReadingId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldIndexValue = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    NewIndexValue = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    OldConsumptionKWh = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NewConsumptionKWh = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChangedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterReadingAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeterReadingAuditLogs_MeterReadings_MeterReadingId",
                        column: x => x.MeterReadingId,
                        principalTable: "MeterReadings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentOwners_ApartmentId",
                table: "ApartmentOwners",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentOwners_ApartmentId_ValidFrom",
                table: "ApartmentOwners",
                columns: new[] { "ApartmentId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadingAuditLogs_ChangedOnUtc",
                table: "MeterReadingAuditLogs",
                column: "ChangedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadingAuditLogs_MeterReadingId",
                table: "MeterReadingAuditLogs",
                column: "MeterReadingId");

            // Etap 6.7 — SCD Type 2 (ApartmentOwners): Aralık çakışması yasağı.
            // Aynı dairenin (ApartmentId) malik geçerlilik aralıkları (tstzrange) asla kesişemez;
            // geçmişe dönük sorgularda bir dönem için yalnızca TEK malik çözülebilir.
            migrationBuilder.Sql(
                @"ALTER TABLE ""ApartmentOwners""
                  ADD CONSTRAINT ""UX_ApartmentOwners_NoOverlap""
                  EXCLUDE USING gist (""ApartmentId"" WITH =, tstzrange(""ValidFrom"", ""ValidTo"") WITH &&);");

            // Etap 6.7 — SCD Type 2 (ApartmentOwners): Tek aktif kayıt kuralı.
            // Bir dairenin ValidTo'su NULL olan (hâlâ aktif malik) en fazla 1 kaydı olabilir.
            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""UX_ApartmentOwners_Active""
                  ON ""ApartmentOwners"" (""ApartmentId"")
                  WHERE ""ValidTo"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""ApartmentOwners"" DROP CONSTRAINT IF EXISTS ""UX_ApartmentOwners_NoOverlap"";");

            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""UX_ApartmentOwners_Active"";");

            migrationBuilder.DropTable(
                name: "ApartmentOwners");

            migrationBuilder.DropTable(
                name: "MeterReadingAuditLogs");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "GasInvoices");
        }
    }
}
