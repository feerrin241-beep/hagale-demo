using Hagale.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hagale.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HagaleDbContext))]
[Migration("20261021100000_AddPersonalDriverDispatchTurns")]
public sealed class AddPersonalDriverDispatchTurns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "HasResponded",
            table: "RideDriverOffers",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.CreateIndex(
            name: "IX_RideDriverOffers_DriverProfileId_PersonalTurn",
            table: "RideDriverOffers",
            column: "DriverProfileId",
            unique: true,
            filter: "[Status] = N'Pending' AND [HasResponded] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RideDriverOffers_DriverProfileId_PersonalTurn",
            table: "RideDriverOffers");

        migrationBuilder.DropColumn(
            name: "HasResponded",
            table: "RideDriverOffers");
    }
}
