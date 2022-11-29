using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace QQBot.EntityFrameworkCore.Migrations
{
    public partial class removeaidraw : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ai.DrawLimit");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ai.DrawLimit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UId = table.Column<long>(type: "bigint", nullable: false),
                    UsedInPrivate = table.Column<int>(type: "int", nullable: false),
                    UsedInPublic = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ai.DrawLimit", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ai.DrawLimit_UId_DateTime",
                table: "Ai.DrawLimit",
                columns: new[] { "UId", "DateTime" });
        }
    }
}
