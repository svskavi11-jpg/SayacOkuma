using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Etap68SplitOpeningBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OpeningBalance",
                table: "Apartments",
                newName: "OpeningBalancePrincipal");

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBalanceInterest",
                table: "Apartments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpeningBalanceInterest",
                table: "Apartments");

            migrationBuilder.RenameColumn(
                name: "OpeningBalancePrincipal",
                table: "Apartments",
                newName: "OpeningBalance");
        }
    }
}
