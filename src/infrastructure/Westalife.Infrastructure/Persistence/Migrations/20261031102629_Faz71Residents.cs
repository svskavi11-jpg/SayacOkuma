using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Faz71Residents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Faz 7.1 — Hibrit Model (KMK m.22): Eski SCD kısıtları KALDIRILIR;
            // Ev Sahibi + Kiracı aynı anda aktif olabilmesi için ResidentType kapsamlı
            // yeni kısıtlar migration sonunda tekrar oluşturulur.
            migrationBuilder.Sql(
                @"ALTER TABLE ""ApartmentOwners"" DROP CONSTRAINT IF EXISTS ""UX_ApartmentOwners_NoOverlap"";");

            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""UX_ApartmentOwners_Active"";");

            migrationBuilder.AddColumn<Guid>(
                name: "ApartmentOwnerId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LegalResponsibleOwnerId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetDebtType",
                table: "Payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "ApartmentOwners",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityNumber",
                table: "ApartmentOwners",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ApartmentOwners",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "ApartmentOwners",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResidentType",
                table: "ApartmentOwners",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "ApartmentOwnerId",
                table: "ApartmentInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LegalResponsibleOwnerId",
                table: "ApartmentInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApartmentOwnerId",
                table: "AidatCharges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LegalResponsibleOwnerId",
                table: "AidatCharges",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ApartmentOwnerId",
                table: "Payments",
                column: "ApartmentOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_LegalResponsibleOwnerId",
                table: "Payments",
                column: "LegalResponsibleOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoices_ApartmentOwnerId",
                table: "ApartmentInvoices",
                column: "ApartmentOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentInvoices_LegalResponsibleOwnerId",
                table: "ApartmentInvoices",
                column: "LegalResponsibleOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AidatCharges_ApartmentOwnerId",
                table: "AidatCharges",
                column: "ApartmentOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AidatCharges_LegalResponsibleOwnerId",
                table: "AidatCharges",
                column: "LegalResponsibleOwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_AidatCharges_ApartmentOwners_ApartmentOwnerId",
                table: "AidatCharges",
                column: "ApartmentOwnerId",
                principalTable: "ApartmentOwners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AidatCharges_ApartmentOwners_LegalResponsibleOwnerId",
                table: "AidatCharges",
                column: "LegalResponsibleOwnerId",
                principalTable: "ApartmentOwners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApartmentInvoices_ApartmentOwners_ApartmentOwnerId",
                table: "ApartmentInvoices",
                column: "ApartmentOwnerId",
                principalTable: "ApartmentOwners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApartmentInvoices_ApartmentOwners_LegalResponsibleOwnerId",
                table: "ApartmentInvoices",
                column: "LegalResponsibleOwnerId",
                principalTable: "ApartmentOwners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_ApartmentOwners_ApartmentOwnerId",
                table: "Payments",
                column: "ApartmentOwnerId",
                principalTable: "ApartmentOwners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_ApartmentOwners_LegalResponsibleOwnerId",
                table: "Payments",
                column: "LegalResponsibleOwnerId",
                principalTable: "ApartmentOwners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Faz 7.1 — Hibrit Model: ResidentType kapsamlı SCD kısıtları.
            // Kural 1: Aynı daire + aynı türün zaman aralıkları çakışamaz
            // (farklı türler — Ev Sahibi + Kiracı — aynı anda aktif olabilir).
            migrationBuilder.Sql(
                @"ALTER TABLE ""ApartmentOwners""
                  ADD CONSTRAINT ""UX_ApartmentOwners_NoOverlap""
                  EXCLUDE USING gist (""ApartmentId"" WITH =, ""ResidentType"" WITH =, tstzrange(""ValidFrom"", ""ValidTo"") WITH &&);");

            // Kural 2: Aynı daire + aynı türün ValidTo'su NULL olan en fazla 1 kaydı vardır.
            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""UX_ApartmentOwners_Active""
                  ON ""ApartmentOwners"" (""ApartmentId"", ""ResidentType"")
                  WHERE ""ValidTo"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Faz 7.1 geri alma: Tip-kapsamlı kısıtları kaldır, eski (yalnız malik) kısıtları
            // geri getir. Not: Geri alma öncesi Kiracı kayıtları varsa eski kısıtlar çakışır —
            // bu nedenle Kiracı (ResidentType=2) kayıtları güvenli şekilde silinir.
            migrationBuilder.Sql(
                @"ALTER TABLE ""ApartmentOwners"" DROP CONSTRAINT IF EXISTS ""UX_ApartmentOwners_NoOverlap"";");

            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""UX_ApartmentOwners_Active"";");

            migrationBuilder.Sql(
                @"DELETE FROM ""ApartmentOwners"" WHERE ""ResidentType"" = 2;");

            migrationBuilder.DropForeignKey(
                name: "FK_AidatCharges_ApartmentOwners_ApartmentOwnerId",
                table: "AidatCharges");

            migrationBuilder.DropForeignKey(
                name: "FK_AidatCharges_ApartmentOwners_LegalResponsibleOwnerId",
                table: "AidatCharges");

            migrationBuilder.DropForeignKey(
                name: "FK_ApartmentInvoices_ApartmentOwners_ApartmentOwnerId",
                table: "ApartmentInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_ApartmentInvoices_ApartmentOwners_LegalResponsibleOwnerId",
                table: "ApartmentInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_ApartmentOwners_ApartmentOwnerId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_ApartmentOwners_LegalResponsibleOwnerId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ApartmentOwnerId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_LegalResponsibleOwnerId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_ApartmentInvoices_ApartmentOwnerId",
                table: "ApartmentInvoices");

            migrationBuilder.DropIndex(
                name: "IX_ApartmentInvoices_LegalResponsibleOwnerId",
                table: "ApartmentInvoices");

            migrationBuilder.DropIndex(
                name: "IX_AidatCharges_ApartmentOwnerId",
                table: "AidatCharges");

            migrationBuilder.DropIndex(
                name: "IX_AidatCharges_LegalResponsibleOwnerId",
                table: "AidatCharges");

            migrationBuilder.DropColumn(
                name: "ApartmentOwnerId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "LegalResponsibleOwnerId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TargetDebtType",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "ApartmentOwners");

            migrationBuilder.DropColumn(
                name: "IdentityNumber",
                table: "ApartmentOwners");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ApartmentOwners");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "ApartmentOwners");

            migrationBuilder.DropColumn(
                name: "ResidentType",
                table: "ApartmentOwners");

            migrationBuilder.DropColumn(
                name: "ApartmentOwnerId",
                table: "ApartmentInvoices");

            migrationBuilder.DropColumn(
                name: "LegalResponsibleOwnerId",
                table: "ApartmentInvoices");

            migrationBuilder.DropColumn(
                name: "ApartmentOwnerId",
                table: "AidatCharges");

            migrationBuilder.DropColumn(
                name: "LegalResponsibleOwnerId",
                table: "AidatCharges");

            // Eski (Faz 7 öncesi) SCD kısıtları geri yüklenir — yalnız malik modeli.
            migrationBuilder.Sql(
                @"ALTER TABLE ""ApartmentOwners""
                  ADD CONSTRAINT ""UX_ApartmentOwners_NoOverlap""
                  EXCLUDE USING gist (""ApartmentId"" WITH =, tstzrange(""ValidFrom"", ""ValidTo"") WITH &&);");

            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""UX_ApartmentOwners_Active""
                  ON ""ApartmentOwners"" (""ApartmentId"")
                  WHERE ""ValidTo"" IS NULL;");
        }
    }
}
