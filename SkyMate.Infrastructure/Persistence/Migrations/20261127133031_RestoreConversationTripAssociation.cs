using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestoreConversationTripAssociation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TripId",
                table: "AiConversations",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Trips_Id_UserId",
                table: "Trips",
                columns: new[] { "Id", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_TripId_UserId_CreatedAt",
                table: "AiConversations",
                columns: new[] { "TripId", "UserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_AiConversations_Trips_TripId_UserId",
                table: "AiConversations",
                columns: new[] { "TripId", "UserId" },
                principalTable: "Trips",
                principalColumns: new[] { "Id", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiConversations_Trips_TripId_UserId",
                table: "AiConversations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Trips_Id_UserId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_AiConversations_TripId_UserId_CreatedAt",
                table: "AiConversations");

            migrationBuilder.DropColumn(
                name: "TripId",
                table: "AiConversations");
        }
    }
}
