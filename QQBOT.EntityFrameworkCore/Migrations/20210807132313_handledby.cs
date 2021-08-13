using Microsoft.EntityFrameworkCore.Migrations;

namespace QQBOT.EntityFrameworkCore.Migrations
{
    public partial class handledby : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HandledBy",
                table: "AuditLog",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HandledBy",
                table: "AuditLog");
        }
    }
}
