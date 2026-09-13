using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hagale.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRideWaitingPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdditionalWaitingFarePerMinuteCopAtStart",
                table: "RideRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdditionalWaitingMinutes",
                table: "RideRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IncludedWaitingMinutesAtStart",
                table: "RideRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WaitingAdditionalChargeCop",
                table: "RideRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WaitingEndedAtUtc",
                table: "RideRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WaitingStartedAtUtc",
                table: "RideRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdditionalWaitingFarePerMinuteCop",
                table: "PricingRules",
                type: "int",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.AddColumn<int>(
                name: "IncludedWaitingMinutes",
                table: "PricingRules",
                type: "int",
                nullable: false,
                defaultValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalWaitingFarePerMinuteCopAtStart",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "AdditionalWaitingMinutes",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "IncludedWaitingMinutesAtStart",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "WaitingAdditionalChargeCop",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "WaitingEndedAtUtc",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "WaitingStartedAtUtc",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "AdditionalWaitingFarePerMinuteCop",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "IncludedWaitingMinutes",
                table: "PricingRules");
        }
    }
}
