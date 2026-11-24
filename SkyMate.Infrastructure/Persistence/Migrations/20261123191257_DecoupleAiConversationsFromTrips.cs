using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleAiConversationsFromTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropIndex(
                name: "IX_AiConversations_UserId_CreatedAt",
                table: "AiConversations");

            migrationBuilder.DropColumn(
                name: "TripId",
                table: "AiConversations");

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_UserId_UpdatedAt",
                table: "AiConversations",
                columns: new[] { "UserId", "UpdatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_AiConversations_AspNetUsers_UserId",
                table: "AiConversations",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removed TripId values cannot be restored accurately after the migration runs.
            // Refuse rollback instead of assigning every conversation a fabricated TripId.
            throw new System.NotSupportedException(
                "DecoupleAiConversationsFromTrips is irreversible because it removes the former TripId relationship.");
        }
    }
}
