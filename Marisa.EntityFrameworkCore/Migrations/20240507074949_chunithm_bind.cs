using Microsoft.EntityFrameworkCore.Migrations;

namespace QQBot.EntityFrameworkCore.Migrations
{
    public partial class chunithm_bind : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Chunithm.Bind",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UId = table.Column<long>(type: "bigint", nullable: false),
                    AccessCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ServerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chunithm.Bind", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chunithm.Bind_UId",
                table: "Chunithm.Bind",
                column: "UId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Chunithm.Bind");
        }
    }
}
