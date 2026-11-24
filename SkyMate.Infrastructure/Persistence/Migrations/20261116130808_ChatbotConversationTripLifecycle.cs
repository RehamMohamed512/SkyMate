using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyMate.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migration تربط كل محادثة برحلتها وتضيف مفتاحًا مركبًا يطابق مالك المحادثة بمالك الرحلة.
    /// فحص Up يوقف التطبيق لو وُجدت محادثات قديمة قبل تعيين TripId غير معروف.
    /// </summary>
    public partial class ChatbotConversationTripLifecycle : Migration
    {
        /// <summary>يضيف TripId الإلزامي وعلاقة الرحلة والفهرس بعد فحص البيانات القديمة.</summary>
        /// <param name="migrationBuilder">أوامر بناء schema التي ينفذها EF Core.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ما بنخمنش رحلة المحادثات القديمة؛ THROW يوقف التغيير ويحافظ على بياناتها كما هي.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [AiConversations]) " +
                "THROW 51000, 'ChatbotConversationTripLifecycle requires each existing conversation to be assigned to its correct trip before migration.', 1;");

            migrationBuilder.AddColumn<int>(
                name: "TripId",
                table: "AiConversations",
                type: "int",
                nullable: false);

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
                principalColumns: new[] { "Id", "UserId" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <summary>يعكس تغييرات schema الخاصة بهذه Migration عند طلب rollback صريح.</summary>
        /// <param name="migrationBuilder">أوامر عكس schema التي ينفذها EF Core.</param>
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
