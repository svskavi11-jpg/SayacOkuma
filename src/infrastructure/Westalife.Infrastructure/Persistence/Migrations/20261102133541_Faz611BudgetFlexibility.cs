using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Faz611BudgetFlexibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                table: "Expenses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FundAidatInvoiceId",
                table: "Expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChargeType",
                table: "AidatInvoices",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetAmount",
                table: "AidatInvoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "AidatInvoices",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BudgetPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    LockedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FromBudgetItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToBudgetItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetTransfers_BudgetItems_FromBudgetItemId",
                        column: x => x.FromBudgetItemId,
                        principalTable: "BudgetItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetTransfers_BudgetItems_ToBudgetItemId",
                        column: x => x.ToBudgetItemId,
                        principalTable: "BudgetItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncomeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EstimatedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_FundAidatInvoiceId",
                table: "Expenses",
                column: "FundAidatInvoiceId");

            migrationBuilder.CreateIndex(
                name: "UX_BudgetPeriods_Year",
                table: "BudgetPeriods",
                column: "Year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTransfers_FromBudgetItemId",
                table: "BudgetTransfers",
                column: "FromBudgetItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTransfers_ToBudgetItemId",
                table: "BudgetTransfers",
                column: "ToBudgetItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTransfers_TransferDate",
                table: "BudgetTransfers",
                column: "TransferDate");

            migrationBuilder.CreateIndex(
                name: "UX_IncomeItems_Year_Category",
                table: "IncomeItems",
                columns: new[] { "Year", "Category" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_AidatInvoices_FundAidatInvoiceId",
                table: "Expenses",
                column: "FundAidatInvoiceId",
                principalTable: "AidatInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_AidatInvoices_FundAidatInvoiceId",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "BudgetPeriods");

            migrationBuilder.DropTable(
                name: "BudgetTransfers");

            migrationBuilder.DropTable(
                name: "IncomeItems");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_FundAidatInvoiceId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "FundAidatInvoiceId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ChargeType",
                table: "AidatInvoices");

            migrationBuilder.DropColumn(
                name: "TargetAmount",
                table: "AidatInvoices");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "AidatInvoices");
        }
    }
}
