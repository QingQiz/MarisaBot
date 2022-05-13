using Microsoft.EntityFrameworkCore.Migrations;

namespace QQBot.EntityFrameworkCore.Migrations
{
    public partial class osubindindex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Osu.Bind_OsuUserId",
                table: "Osu.Bind",
                column: "OsuUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Osu.Bind_UserId",
                table: "Osu.Bind",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Osu.Bind_OsuUserId",
                table: "Osu.Bind");

            migrationBuilder.DropIndex(
                name: "IX_Osu.Bind_UserId",
                table: "Osu.Bind");
        }
    }
}
