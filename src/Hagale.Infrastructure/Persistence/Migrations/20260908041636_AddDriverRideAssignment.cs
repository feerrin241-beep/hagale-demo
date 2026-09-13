using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hagale.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverRideAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcceptedAtUtc",
                table: "RideRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedDriverProfileId",
                table: "RideRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RideRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_AssignedDriverProfileId",
                table: "RideRequests",
                column: "AssignedDriverProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RideRequests_Status_OperatingCityCode_ServiceType",
                table: "RideRequests",
                columns: new[] { "Status", "OperatingCityCode", "ServiceType" });

            migrationBuilder.AddForeignKey(
                name: "FK_RideRequests_DriverProfiles_AssignedDriverProfileId",
                table: "RideRequests",
                column: "AssignedDriverProfileId",
                principalTable: "DriverProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RideRequests_DriverProfiles_AssignedDriverProfileId",
                table: "RideRequests");

            migrationBuilder.DropIndex(
                name: "IX_RideRequests_AssignedDriverProfileId",
                table: "RideRequests");

            migrationBuilder.DropIndex(
                name: "IX_RideRequests_Status_OperatingCityCode_ServiceType",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "AcceptedAtUtc",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "AssignedDriverProfileId",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RideRequests");
        }
    }
}
