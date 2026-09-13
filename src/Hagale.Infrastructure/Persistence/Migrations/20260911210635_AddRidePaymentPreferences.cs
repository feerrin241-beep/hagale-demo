using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hagale.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRidePaymentPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FareMode",
                table: "RideRequests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PassengerOffer");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "RideRequests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Cash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FareMode",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "RideRequests");
        }
    }
}
