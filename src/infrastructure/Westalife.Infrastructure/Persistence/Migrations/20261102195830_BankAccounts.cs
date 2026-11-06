using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BankAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "Expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    Iban = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpeningDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BankAccountId",
                table: "Payments",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BankAccountId",
                table: "Expenses",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_AccountNumber",
                table: "BankAccounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_Iban",
                table: "BankAccounts",
                column: "Iban",
                unique: true,
                filter: "\"Iban\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_BankAccounts_BankAccountId",
                table: "Expenses",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_BankAccounts_BankAccountId",
                table: "Payments",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_BankAccounts_BankAccountId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_BankAccounts_BankAccountId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Payments_BankAccountId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BankAccountId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "Expenses");
        }
    }
}
