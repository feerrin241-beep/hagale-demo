using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hagale.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DestinationLatitude",
                table: "RideRequests",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DestinationLongitude",
                table: "RideRequests",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PickupLatitude",
                table: "RideRequests",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PickupLongitude",
                table: "RideRequests",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastKnownLatitude",
                table: "DriverProfiles",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastKnownLongitude",
                table: "DriverProfiles",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LocationUpdatedAtUtc",
                table: "DriverProfiles",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DestinationLatitude",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "DestinationLongitude",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "PickupLatitude",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "PickupLongitude",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "LastKnownLatitude",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "LastKnownLongitude",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "LocationUpdatedAtUtc",
                table: "DriverProfiles");
        }
    }
}
