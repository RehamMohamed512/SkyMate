using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantKnowledgeBaseAndProviderContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderContextJson",
                table: "AiConversationMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeedKey = table.Column<string>(type: "nvarchar(205)", maxLength: 205, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseEntries_IsActive_Id",
                table: "KnowledgeBaseEntries",
                columns: new[] { "IsActive", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseEntries_SeedKey",
                table: "KnowledgeBaseEntries",
                column: "SeedKey",
                unique: true,
                filter: "[SeedKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseEntries_Title",
                table: "KnowledgeBaseEntries",
                column: "Title",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeBaseEntries");

            migrationBuilder.DropColumn(
                name: "ProviderContextJson",
                table: "AiConversationMessages");
        }
    }
}
