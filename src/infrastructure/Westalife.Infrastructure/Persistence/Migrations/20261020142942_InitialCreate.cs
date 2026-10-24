using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FixedCostRatio = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GasInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubscriberNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MasterMeterSerial = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReadingDateFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadingDateTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IssuedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrectedVolumeM3 = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalEnergyKWh = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PressureCorrectionFactor = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    ConsumptionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ConsumptionVat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DistributionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DistributionVat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GasInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Apartments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoorNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Apartments_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApartmentDimensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SquareMeters = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    CommonAreaRatio = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentDimensions", x => x.Id);
                    table.CheckConstraint("CK_ApartmentDimensions_Period", "\"ValidFrom\" < COALESCE(\"ValidTo\", 'infinity'::timestamptz)");
                    table.ForeignKey(
                        name: "FK_ApartmentDimensions_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Manufacturer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstallationDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meters_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeterReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeterId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndexValue = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ConsumptionKWh = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReadingDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeterReadings_Meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "Meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentDimensions_ApartmentId",
                table: "ApartmentDimensions",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentDimensions_ApartmentId_ValidFrom",
                table: "ApartmentDimensions",
                columns: new[] { "ApartmentId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_BuildingId_DoorNumber",
                table: "Apartments",
                columns: new[] { "BuildingId", "DoorNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GasInvoices_ReadingPeriod",
                table: "GasInvoices",
                columns: new[] { "ReadingDateFrom", "ReadingDateTo" });

            migrationBuilder.CreateIndex(
                name: "UX_GasInvoices_InvoiceNumber",
                table: "GasInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_IsProcessed",
                table: "MeterReadings",
                column: "IsProcessed");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_ReadingDateUtc",
                table: "MeterReadings",
                column: "ReadingDateUtc");

            migrationBuilder.CreateIndex(
                name: "UX_MeterReadings_MeterId_ReadingDateUtc",
                table: "MeterReadings",
                columns: new[] { "MeterId", "ReadingDateUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meters_ApartmentId",
                table: "Meters",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "UX_Meters_SerialNumber",
                table: "Meters",
                column: "SerialNumber",
                unique: true);

            // ==========================================================================
            // Finansal ve SCD Type 2 bütünlük kısıtları (EF Core native desteklemediği
            // için raw SQL olarak uygulanır — PostgreSQL 18, ICU tr-TR)
            // ==========================================================================

            // 0) btree_gist: UUID alanları üzerinde exclusion constraint için gerekli.
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // 1) SCD Type 2 — Aralık çakışması yasağı:
            // Aynı dairenin (ApartmentId) geçerlilik aralıkları (tstzrange) asla kesişemez.
            // Böylece geçmişe dönük hesaplamalarda bir dönem için yalnızca TEK m² değeri çözülebilir.
            migrationBuilder.Sql(
                @"ALTER TABLE ""ApartmentDimensions""
                  ADD CONSTRAINT ""UX_ApartmentDimensions_NoOverlap""
                  EXCLUDE USING gist (""ApartmentId"" WITH =, tstzrange(""ValidFrom"", ""ValidTo"") WITH &&);");

            // 2) SCD Type 2 — Tek aktif kayıt kuralı:
            // Bir dairenin ValidTo'su NULL olan (hâlâ geçerli) en fazla 1 kaydı olabilir.
            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""UX_ApartmentDimensions_Active""
                  ON ""ApartmentDimensions"" (""ApartmentId"")
                  WHERE ""ValidTo"" IS NULL;");

            // 3) Fatura dönemi çakışması yasağı:
            // Aynı (veya kesişen) okuma dönemi için iki fatura girilemez — kuruşu kuruşuna
            // mutabakatın ön koşulu olan "her dönem için tek fatura" garantisidir.
            migrationBuilder.Sql(
                @"ALTER TABLE ""GasInvoices""
                  ADD CONSTRAINT ""UX_GasInvoices_NoPeriodOverlap""
                  EXCLUDE USING gist (tstzrange(""ReadingDateFrom"", ""ReadingDateTo"") WITH &&);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""UX_ApartmentDimensions_Active"";");
            migrationBuilder.Sql(@"ALTER TABLE ""ApartmentDimensions"" DROP CONSTRAINT IF EXISTS ""UX_ApartmentDimensions_NoOverlap"";");
            migrationBuilder.Sql(@"ALTER TABLE ""GasInvoices"" DROP CONSTRAINT IF EXISTS ""UX_GasInvoices_NoPeriodOverlap"";");

            migrationBuilder.DropTable(
                name: "ApartmentDimensions");

            migrationBuilder.DropTable(
                name: "GasInvoices");

            migrationBuilder.DropTable(
                name: "MeterReadings");

            migrationBuilder.DropTable(
                name: "Meters");

            migrationBuilder.DropTable(
                name: "Apartments");

            migrationBuilder.DropTable(
                name: "Buildings");
        }
    }
}
