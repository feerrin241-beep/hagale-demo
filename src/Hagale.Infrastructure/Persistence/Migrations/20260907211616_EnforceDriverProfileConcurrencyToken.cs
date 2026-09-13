using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hagale.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDriverProfileConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DriverProfiles");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DriverProfiles",
                type: "rowversion",
                rowVersion: true,
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DriverProfiles");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DriverProfiles",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }
    }
}
