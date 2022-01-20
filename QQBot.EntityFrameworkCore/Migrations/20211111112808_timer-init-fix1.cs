using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace QQBot.EntityFrameworkCore.Migrations
{
    public partial class timerinitfix1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Timer",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TimeBegin",
                table: "Timer",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "TimeEnd",
                table: "Timer",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Uid",
                table: "Timer",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Timer");

            migrationBuilder.DropColumn(
                name: "TimeBegin",
                table: "Timer");

            migrationBuilder.DropColumn(
                name: "TimeEnd",
                table: "Timer");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Timer");
        }
    }
}
