using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoleAssignmentsAndWaiverOffsets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RoleType = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExemptionRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BoardDecisionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleAssignments_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WaiverOffsets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AidatChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OffsetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaiverOffsets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaiverOffsets_AidatCharges_AidatChargeId",
                        column: x => x.AidatChargeId,
                        principalTable: "AidatCharges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaiverOffsets_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaiverOffsets_BudgetItems_BudgetItemId",
                        column: x => x.BudgetItemId,
                        principalTable: "BudgetItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaiverOffsets_RoleAssignments_RoleAssignmentId",
                        column: x => x.RoleAssignmentId,
                        principalTable: "RoleAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_RoleAssignments_OpenApartment",
                table: "RoleAssignments",
                column: "ApartmentId",
                unique: true,
                filter: "\"EndDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WaiverOffsets_ApartmentId",
                table: "WaiverOffsets",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WaiverOffsets_BudgetItemId",
                table: "WaiverOffsets",
                column: "BudgetItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WaiverOffsets_RoleAssignmentId",
                table: "WaiverOffsets",
                column: "RoleAssignmentId");

            migrationBuilder.CreateIndex(
                name: "UX_WaiverOffsets_AidatChargeId",
                table: "WaiverOffsets",
                column: "AidatChargeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaiverOffsets");

            migrationBuilder.DropTable(
                name: "RoleAssignments");
        }
    }
}
