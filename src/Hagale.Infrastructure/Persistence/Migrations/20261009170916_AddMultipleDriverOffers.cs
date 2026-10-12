using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hagale.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleDriverOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OfferSearchRound",
                table: "RideRequests",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OfferWindowEndsAtUtc",
                table: "RideRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FairOfferMinimumPercent",
                table: "PricingRules",
                type: "int",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.AddColumn<int>(
                name: "FavorableOfferMinimumPercent",
                table: "PricingRules",
                type: "int",
                nullable: false,
                defaultValue: 105);

            migrationBuilder.CreateTable(
                name: "PlatformAppearances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    AccentColor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ActionColor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BusyColor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CustomerModeLabel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DriverModeLabel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FreeStatusLabel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BusyStatusLabel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequestActionLabel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DriverOfferVoiceTemplate = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAppearances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RideChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RideRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RideChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RideChatMessages_RideRequests_RideRequestId",
                        column: x => x.RideRequestId,
                        principalTable: "RideRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RideDriverOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RideRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferedPriceCop = table.Column<int>(type: "int", nullable: false),
                    OfferedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SearchRound = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RideDriverOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RideDriverOffers_DriverProfiles_DriverProfileId",
                        column: x => x.DriverProfileId,
                        principalTable: "DriverProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RideDriverOffers_RideRequests_RideRequestId",
                        column: x => x.RideRequestId,
                        principalTable: "RideRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RideRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RideRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaterRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RatedRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    RatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RideRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RideRatings_RideRequests_RideRequestId",
                        column: x => x.RideRequestId,
                        principalTable: "RideRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoredDocumentBlobs",
                columns: table => new
                {
                    StorageObjectKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredDocumentBlobs", x => x.StorageObjectKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RideChatMessages_RideRequestId_SentAtUtc",
                table: "RideChatMessages",
                columns: new[] { "RideRequestId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RideChatMessages_SenderUserId",
                table: "RideChatMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RideDriverOffers_DriverProfileId_Status_ExpiresAtUtc",
                table: "RideDriverOffers",
                columns: new[] { "DriverProfileId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RideDriverOffers_RideRequestId_DriverProfileId_SearchRound",
                table: "RideDriverOffers",
                columns: new[] { "RideRequestId", "DriverProfileId", "SearchRound" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RideDriverOffers_RideRequestId_Status_SearchRound",
                table: "RideDriverOffers",
                columns: new[] { "RideRequestId", "Status", "SearchRound" });

            migrationBuilder.CreateIndex(
                name: "IX_RideRatings_RideRequestId_RatedAtUtc",
                table: "RideRatings",
                columns: new[] { "RideRequestId", "RatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RideRatings_RideRequestId_RaterUserId_RaterRole",
                table: "RideRatings",
                columns: new[] { "RideRequestId", "RaterUserId", "RaterRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoredDocumentBlobs_OwnerUserId",
                table: "StoredDocumentBlobs",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformAppearances");

            migrationBuilder.DropTable(
                name: "RideChatMessages");

            migrationBuilder.DropTable(
                name: "RideDriverOffers");

            migrationBuilder.DropTable(
                name: "RideRatings");

            migrationBuilder.DropTable(
                name: "StoredDocumentBlobs");

            migrationBuilder.DropColumn(
                name: "OfferSearchRound",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "OfferWindowEndsAtUtc",
                table: "RideRequests");

            migrationBuilder.DropColumn(
                name: "FairOfferMinimumPercent",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "FavorableOfferMinimumPercent",
                table: "PricingRules");
        }
    }
}
