using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Westalife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleAssignmentAndWaiverCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_WaiverOffsets_AidatChargeId",
                table: "WaiverOffsets");

            migrationBuilder.DropIndex(
                name: "UX_RoleAssignments_OpenApartment",
                table: "RoleAssignments");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "WaiverOffsets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "WaiverOffsets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "RoleAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "RoleAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "UX_WaiverOffsets_AidatChargeId",
                table: "WaiverOffsets",
                column: "AidatChargeId",
                unique: true,
                filter: "\"IsCancelled\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UX_RoleAssignments_OpenApartment",
                table: "RoleAssignments",
                column: "ApartmentId",
                unique: true,
                filter: "\"EndDate\" IS NULL AND \"IsCancelled\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_WaiverOffsets_AidatChargeId",
                table: "WaiverOffsets");

            migrationBuilder.DropIndex(
                name: "UX_RoleAssignments_OpenApartment",
                table: "RoleAssignments");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "WaiverOffsets");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "WaiverOffsets");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "RoleAssignments");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "RoleAssignments");

            migrationBuilder.CreateIndex(
                name: "UX_WaiverOffsets_AidatChargeId",
                table: "WaiverOffsets",
                column: "AidatChargeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_RoleAssignments_OpenApartment",
                table: "RoleAssignments",
                column: "ApartmentId",
                unique: true,
                filter: "\"EndDate\" IS NULL");
        }
    }
}
