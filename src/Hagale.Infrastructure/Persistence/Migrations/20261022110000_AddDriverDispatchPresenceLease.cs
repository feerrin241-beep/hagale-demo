using Hagale.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hagale.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HagaleDbContext))]
[Migration("20261022110000_AddDriverDispatchPresenceLease")]
public sealed class AddDriverDispatchPresenceLease : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DispatchPresenceExpiresAtUtc",
            table: "DriverProfiles",
            type: "datetimeoffset",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DispatchPresenceExpiresAtUtc",
            table: "DriverProfiles");
    }
}
