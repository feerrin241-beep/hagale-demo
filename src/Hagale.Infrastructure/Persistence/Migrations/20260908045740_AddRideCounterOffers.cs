using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hagale.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRideCounterOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CounterOfferAtUtc",
                table: "RideRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CounterOfferPriceCop",
                table: "RideRequests",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CounterOfferAtUtc",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "CounterOfferPriceCop",
                table: "RideRequests");
        }
    }
}
