using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hagale.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurablePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinimumFareCopAtRequest",
                table: "RideRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingCityCode",
                table: "RideRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProposedPriceCop",
                table: "RideRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PricingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CityCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ServiceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MinimumFareCop = table.Column<int>(type: "int", nullable: false),
                    BaseFareCop = table.Column<int>(type: "int", nullable: false),
                    FarePerKilometerCop = table.Column<int>(type: "int", nullable: false),
                    FarePerMinuteCop = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_CityCode_ServiceType",
                table: "PricingRules",
                columns: new[] { "CityCode", "ServiceType" },
                unique: true);

            migrationBuilder.Sql("""
                UPDATE [RideRequests]
                SET [MinimumFareCopAtRequest] = 3500,
                    [ProposedPriceCop] = 3500,
                    [OperatingCityCode] = 'BUC'
                WHERE [MinimumFareCopAtRequest] IS NULL
                   OR [ProposedPriceCop] IS NULL
                   OR [OperatingCityCode] IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "MinimumFareCopAtRequest",
                table: "RideRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OperatingCityCode",
                table: "RideRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProposedPriceCop",
                table: "RideRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingRules");

            migrationBuilder.DropColumn(
                name: "MinimumFareCopAtRequest",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "OperatingCityCode",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "ProposedPriceCop",
                table: "RideRequests");
        }
    }
}
