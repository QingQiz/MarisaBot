using Microsoft.EntityFrameworkCore.Migrations;

namespace QQBot.EntityFrameworkCore.Migrations
{
    public partial class mai_bind : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaiMaiDx.Bind",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UId = table.Column<long>(type: "bigint", nullable: false),
                    AimeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaiMaiDx.Bind", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaiMaiDx.Bind_UId",
                table: "MaiMaiDx.Bind",
                column: "UId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaiMaiDx.Bind");
        }
    }
}
