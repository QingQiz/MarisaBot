using Microsoft.EntityFrameworkCore.Migrations;

namespace QQBot.EntityFrameworkCore.Migrations
{
    public partial class chunithmguess : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Chunithm.Guess",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimesStart = table.Column<int>(type: "int", nullable: false),
                    TimesCorrect = table.Column<int>(type: "int", nullable: false),
                    TimesWrong = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chunithm.Guess", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chunithm.Guess_UId",
                table: "Chunithm.Guess",
                column: "UId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Chunithm.Guess");
        }
    }
}
