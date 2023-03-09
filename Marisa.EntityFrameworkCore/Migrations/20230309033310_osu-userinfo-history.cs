using Microsoft.EntityFrameworkCore.Migrations;

namespace QQBot.EntityFrameworkCore.Migrations
{
    public partial class osuuserinfohistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Osu.UserHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    OsuUserName = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    OsuUserId = table.Column<long>(type: "bigint", nullable: false),
                    UserInfo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Osu.UserHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Osu.UserHistory_OsuUserId",
                table: "Osu.UserHistory",
                column: "OsuUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Osu.UserHistory_OsuUserName",
                table: "Osu.UserHistory",
                column: "OsuUserName");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Osu.UserHistory");
        }
    }
}
