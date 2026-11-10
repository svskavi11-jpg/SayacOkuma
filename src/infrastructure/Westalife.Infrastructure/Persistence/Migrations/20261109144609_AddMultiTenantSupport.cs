using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenantSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RoleAssignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ResidentDocuments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "NotificationTemplates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "NotificationLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Meters",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "IncomeItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "GasInvoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Expenses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Buildings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BudgetTransfers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BudgetPeriods",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BudgetItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BankAccounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppSettings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Apartments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ApartmentOwners",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AidatTariffs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AidatInvoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000017"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000018"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000019"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000020"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000021"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000022"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000023"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000024"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000025"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000026"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000027"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000028"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000029"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000030"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000031"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000032"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000033"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000034"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000035"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000036"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000037"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000038"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000039"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000040"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000041"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000042"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000043"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000044"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000045"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000046"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000047"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000048"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000049"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000050"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000051"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000052"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000053"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000054"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000055"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000056"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000057"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000058"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000059"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000060"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000061"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000062"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000063"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000064"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000065"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000066"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000067"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000068"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000069"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000070"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000071"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Apartments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000072"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000aa01"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "Buildings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.UpdateData(
                table: "NotificationTemplates",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-00000000aa02"));

            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "Id", "CreatedOn", "IsActive", "LastModifiedOn", "Name" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000aa02"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, null, "Westalife Residence" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_TenantId",
                table: "RoleAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResidentDocuments_TenantId",
                table: "ResidentDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId",
                table: "Payments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_TenantId",
                table: "NotificationTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_TenantId",
                table: "NotificationLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_TenantId",
                table: "Meters",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeItems_TenantId",
                table: "IncomeItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GasInvoices_TenantId",
                table: "GasInvoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_TenantId",
                table: "Expenses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_TenantId",
                table: "Buildings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTransfers_TenantId",
                table: "BudgetTransfers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_TenantId",
                table: "BudgetPeriods",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_TenantId",
                table: "BudgetItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_TenantId",
                table: "BankAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_TenantId",
                table: "AppSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_TenantId",
                table: "Apartments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentOwners_TenantId",
                table: "ApartmentOwners",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AidatTariffs_TenantId",
                table: "AidatTariffs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AidatInvoices_TenantId",
                table: "AidatInvoices",
                column: "TenantId");

            migrationBuilder.Sql("""
                ALTER TABLE "AidatInvoices" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "AidatTariffs" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "ApartmentOwners" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "Apartments" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "AppSettings" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "BankAccounts" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "BudgetItems" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "BudgetPeriods" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "BudgetTransfers" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "Buildings" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "Expenses" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "GasInvoices" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "IncomeItems" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "Meters" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "NotificationLogs" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "NotificationTemplates" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "Payments" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "ResidentDocuments" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE "RoleAssignments" ALTER COLUMN "TenantId" DROP DEFAULT;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_AidatInvoices_Tenants_TenantId",
                table: "AidatInvoices",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AidatTariffs_Tenants_TenantId",
                table: "AidatTariffs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApartmentOwners_Tenants_TenantId",
                table: "ApartmentOwners",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Apartments_Tenants_TenantId",
                table: "Apartments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppSettings_Tenants_TenantId",
                table: "AppSettings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_Tenants_TenantId",
                table: "BankAccounts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetItems_Tenants_TenantId",
                table: "BudgetItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetPeriods_Tenants_TenantId",
                table: "BudgetPeriods",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetTransfers_Tenants_TenantId",
                table: "BudgetTransfers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Buildings_Tenants_TenantId",
                table: "Buildings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Tenants_TenantId",
                table: "Expenses",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GasInvoices_Tenants_TenantId",
                table: "GasInvoices",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IncomeItems_Tenants_TenantId",
                table: "IncomeItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Meters_Tenants_TenantId",
                table: "Meters",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationLogs_Tenants_TenantId",
                table: "NotificationLogs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationTemplates_Tenants_TenantId",
                table: "NotificationTemplates",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Tenants_TenantId",
                table: "Payments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResidentDocuments_Tenants_TenantId",
                table: "ResidentDocuments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleAssignments_Tenants_TenantId",
                table: "RoleAssignments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AidatInvoices_Tenants_TenantId",
                table: "AidatInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_AidatTariffs_Tenants_TenantId",
                table: "AidatTariffs");

            migrationBuilder.DropForeignKey(
                name: "FK_ApartmentOwners_Tenants_TenantId",
                table: "ApartmentOwners");

            migrationBuilder.DropForeignKey(
                name: "FK_Apartments_Tenants_TenantId",
                table: "Apartments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppSettings_Tenants_TenantId",
                table: "AppSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_BankAccounts_Tenants_TenantId",
                table: "BankAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetItems_Tenants_TenantId",
                table: "BudgetItems");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetPeriods_Tenants_TenantId",
                table: "BudgetPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetTransfers_Tenants_TenantId",
                table: "BudgetTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_Buildings_Tenants_TenantId",
                table: "Buildings");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Tenants_TenantId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_GasInvoices_Tenants_TenantId",
                table: "GasInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_IncomeItems_Tenants_TenantId",
                table: "IncomeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Meters_Tenants_TenantId",
                table: "Meters");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationLogs_Tenants_TenantId",
                table: "NotificationLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationTemplates_Tenants_TenantId",
                table: "NotificationTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Tenants_TenantId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_ResidentDocuments_Tenants_TenantId",
                table: "ResidentDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_RoleAssignments_Tenants_TenantId",
                table: "RoleAssignments");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_RoleAssignments_TenantId",
                table: "RoleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ResidentDocuments_TenantId",
                table: "ResidentDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TenantId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_NotificationTemplates_TenantId",
                table: "NotificationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_TenantId",
                table: "NotificationLogs");

            migrationBuilder.DropIndex(
                name: "IX_Meters_TenantId",
                table: "Meters");

            migrationBuilder.DropIndex(
                name: "IX_IncomeItems_TenantId",
                table: "IncomeItems");

            migrationBuilder.DropIndex(
                name: "IX_GasInvoices_TenantId",
                table: "GasInvoices");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_TenantId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Buildings_TenantId",
                table: "Buildings");

            migrationBuilder.DropIndex(
                name: "IX_BudgetTransfers_TenantId",
                table: "BudgetTransfers");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPeriods_TenantId",
                table: "BudgetPeriods");

            migrationBuilder.DropIndex(
                name: "IX_BudgetItems_TenantId",
                table: "BudgetItems");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_TenantId",
                table: "BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_AppSettings_TenantId",
                table: "AppSettings");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_TenantId",
                table: "Apartments");

            migrationBuilder.DropIndex(
                name: "IX_ApartmentOwners_TenantId",
                table: "ApartmentOwners");

            migrationBuilder.DropIndex(
                name: "IX_AidatTariffs_TenantId",
                table: "AidatTariffs");

            migrationBuilder.DropIndex(
                name: "IX_AidatInvoices_TenantId",
                table: "AidatInvoices");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RoleAssignments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ResidentDocuments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Meters");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "IncomeItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GasInvoices");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BudgetTransfers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BudgetPeriods");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ApartmentOwners");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AidatTariffs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AidatInvoices");
        }
    }
}
