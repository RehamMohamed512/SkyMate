using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceTripAndActivityIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [TripDays] GROUP BY [TripId], [Date] HAVING COUNT(*) > 1) " +
                "THROW 51001, 'Cannot enforce unique trip dates while duplicate TripDays dates exist.', 1;");
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [Activities] GROUP BY [DayId], [ActivityOrder] HAVING COUNT(*) > 1) " +
                "THROW 51002, 'Cannot enforce unique activity order while duplicate orders exist within a trip day.', 1;");
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [UserPhones]) " +
                "THROW 51003, 'Cannot drop UserPhones while phone records exist; migrate them first.', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_AiConversations_AspNetUsers_UserId",
                table: "AiConversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Trips_AspNetUsers_UserId",
                table: "Trips");

            migrationBuilder.DropTable(
                name: "UserPhones");

            migrationBuilder.DropIndex(
                name: "IX_Activities_DayId_ActivityOrder",
                table: "Activities");

            migrationBuilder.CreateIndex(
                name: "IX_TripDays_TripId_Date",
                table: "TripDays",
                columns: new[] { "TripId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DayId_ActivityOrder",
                table: "Activities",
                columns: new[] { "DayId", "ActivityOrder" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_AspNetUsers_UserId",
                table: "Trips",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trips_AspNetUsers_UserId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_TripDays_TripId_Date",
                table: "TripDays");

            migrationBuilder.DropIndex(
                name: "IX_Activities_DayId_ActivityOrder",
                table: "Activities");

            migrationBuilder.CreateTable(
                name: "UserPhones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPhones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPhones_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DayId_ActivityOrder",
                table: "Activities",
                columns: new[] { "DayId", "ActivityOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPhones_UserId_PhoneNumber",
                table: "UserPhones",
                columns: new[] { "UserId", "PhoneNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AiConversations_AspNetUsers_UserId",
                table: "AiConversations",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_AspNetUsers_UserId",
                table: "Trips",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
