using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditActorStamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WaiverOffsets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "WaiverOffsets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "RoleAssignments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "RoleAssignments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ResidentDocuments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "ResidentDocuments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Payments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Payments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PaymentAllocations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "PaymentAllocations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "NotificationTemplates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "NotificationTemplates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "NotificationLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "NotificationLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "NoticePdfAccessTokens",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "NoticePdfAccessTokens",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Meters",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Meters",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "MeterReadings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "MeterReadings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChangedBy",
                table: "MeterReadingAuditLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "MeterReadingAuditLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "MeterReadingAuditLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "IncomeItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "IncomeItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "GasInvoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "GasInvoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Expenses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Expenses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Buildings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Buildings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BudgetTransfers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "BudgetTransfers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BudgetPeriods",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "BudgetPeriods",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BudgetItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "BudgetItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BankTransactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "BankTransactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BankAccounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "BankAccounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AppSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "AppSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Apartments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Apartments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ApartmentOwners",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "ApartmentOwners",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ApartmentInvoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "ApartmentInvoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ApartmentDimensions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "ApartmentDimensions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AidatTariffs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "AidatTariffs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AidatInvoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "AidatInvoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AidatCharges",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "AidatCharges",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000001"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000002"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000003"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000004"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000005"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000006"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000007"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000008"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000009"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000010"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000011"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000012"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000013"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000014"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000015"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000016"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000017"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000018"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000019"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000020"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000021"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000022"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000023"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000024"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000025"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000026"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000027"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000028"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000029"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000030"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000031"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000032"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000033"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000034"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000035"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000036"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000037"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000038"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000039"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000040"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000041"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000042"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000043"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000044"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000045"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000046"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000047"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000048"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000049"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000050"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000051"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000052"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000053"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000054"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000055"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000056"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000057"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000058"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000059"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000060"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000061"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000062"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000063"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000064"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000065"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000066"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000067"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000068"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000069"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000070"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000071"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ApartmentDimensions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000072"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000017"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000018"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000019"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000020"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000021"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000022"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000023"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000024"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000025"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000026"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000027"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000028"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000029"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000030"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000031"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000032"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000033"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000034"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000035"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000036"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000037"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000038"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000039"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000040"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000041"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000042"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000043"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000044"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000045"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000046"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000047"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000048"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000049"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000050"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000051"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000052"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000053"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000054"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000055"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000056"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000057"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000058"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000059"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000060"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000061"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000062"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000063"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000064"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000065"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000066"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000067"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000068"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000069"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000070"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000071"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000072"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000aa01"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Buildings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000aa02"),
                columns: new[] { "CreatedBy", "LastModifiedBy" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WaiverOffsets");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "WaiverOffsets");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "RoleAssignments");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "RoleAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ResidentDocuments");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "ResidentDocuments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PaymentAllocations");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "PaymentAllocations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "NoticePdfAccessTokens");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "NoticePdfAccessTokens");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Meters");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Meters");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MeterReadingAuditLogs");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "MeterReadingAuditLogs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "IncomeItems");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "IncomeItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "GasInvoices");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "GasInvoices");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BudgetTransfers");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "BudgetTransfers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BudgetPeriods");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "BudgetPeriods");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ApartmentOwners");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "ApartmentOwners");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ApartmentInvoices");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "ApartmentInvoices");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ApartmentDimensions");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "ApartmentDimensions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AidatTariffs");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "AidatTariffs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AidatInvoices");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "AidatInvoices");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AidatCharges");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "AidatCharges");

            migrationBuilder.AlterColumn<string>(
                name: "ChangedBy",
                table: "MeterReadingAuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }
    }
}
