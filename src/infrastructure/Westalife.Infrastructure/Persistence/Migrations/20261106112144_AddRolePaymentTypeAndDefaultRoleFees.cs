using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolePaymentTypeAndDefaultRoleFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FixedAmount",
                table: "RoleAssignments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentType",
                table: "RoleAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PeriodType",
                table: "RoleAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultAuditorExemptionRate",
                table: "AppSettings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultAuditorFixedAmount",
                table: "AppSettings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DefaultAuditorPaymentType",
                table: "AppSettings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "DefaultAuditorPeriodType",
                table: "AppSettings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultManagerExemptionRate",
                table: "AppSettings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 1.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultManagerFixedAmount",
                table: "AppSettings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DefaultManagerPaymentType",
                table: "AppSettings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "DefaultManagerPeriodType",
                table: "AppSettings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000aa01"),
                columns: new[] { "DefaultAuditorPaymentType", "DefaultAuditorPeriodType", "DefaultManagerExemptionRate", "DefaultManagerPaymentType", "DefaultManagerPeriodType" },
                values: new object[] { 1, 1, 1.00m, 1, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FixedAmount",
                table: "RoleAssignments");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "RoleAssignments");

            migrationBuilder.DropColumn(
                name: "PeriodType",
                table: "RoleAssignments");

            migrationBuilder.DropColumn(
                name: "DefaultAuditorExemptionRate",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultAuditorFixedAmount",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultAuditorPaymentType",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultAuditorPeriodType",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultManagerExemptionRate",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultManagerFixedAmount",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultManagerPaymentType",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultManagerPeriodType",
                table: "AppSettings");
        }
    }
}
