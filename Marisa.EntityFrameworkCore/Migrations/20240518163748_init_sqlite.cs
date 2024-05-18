using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marisa.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class init_sqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Arcaea.Guess",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    TimesStart = table.Column<int>(type: "INTEGER", nullable: false),
                    TimesCorrect = table.Column<int>(type: "INTEGER", nullable: false),
                    TimesWrong = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arcaea.Guess", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlackList",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UId = table.Column<long>(type: "INTEGER", nullable: false),
                    AddTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlackList", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chunithm.Bind",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccessCode = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    ServerName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chunithm.Bind", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chunithm.Guess",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    TimesStart = table.Column<int>(type: "INTEGER", nullable: false),
                    TimesCorrect = table.Column<int>(type: "INTEGER", nullable: false),
                    TimesWrong = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chunithm.Guess", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommandFilter",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupId = table.Column<long>(type: "INTEGER", nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandFilter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaiMaiDx.Bind",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UId = table.Column<long>(type: "INTEGER", nullable: false),
                    AimeId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaiMaiDx.Bind", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaiMaiDx.Guess",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    TimesStart = table.Column<int>(type: "INTEGER", nullable: false),
                    TimesCorrect = table.Column<int>(type: "INTEGER", nullable: false),
                    TimesWrong = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaiMaiDx.Guess", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ongeki.Guess",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    TimesStart = table.Column<int>(type: "INTEGER", nullable: false),
                    TimesCorrect = table.Column<int>(type: "INTEGER", nullable: false),
                    TimesWrong = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ongeki.Guess", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Osu.Bind",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    OsuUserName = table.Column<string>(type: "TEXT", nullable: true),
                    OsuUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    GameMode = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Osu.Bind", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Osu.UserHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    OsuUserName = table.Column<string>(type: "TEXT", nullable: true),
                    OsuUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserInfo = table.Column<string>(type: "TEXT", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Osu.UserHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Arcaea.Guess_UId",
                table: "Arcaea.Guess",
                column: "UId");

            migrationBuilder.CreateIndex(
                name: "IX_BlackList_UId",
                table: "BlackList",
                column: "UId");

            migrationBuilder.CreateIndex(
                name: "IX_Chunithm.Bind_UId",
                table: "Chunithm.Bind",
                column: "UId");

            migrationBuilder.CreateIndex(
                name: "IX_Chunithm.Guess_UId",
                table: "Chunithm.Guess",
                column: "UId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandFilter_GroupId",
                table: "CommandFilter",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MaiMaiDx.Bind_UId",
                table: "MaiMaiDx.Bind",
                column: "UId");

            migrationBuilder.CreateIndex(
                name: "IX_MaiMaiDx.Guess_UId",
                table: "MaiMaiDx.Guess",
                column: "UId");

            migrationBuilder.CreateIndex(
                name: "IX_Ongeki.Guess_UId",
                table: "Ongeki.Guess",
                column: "UId");

            migrationBuilder.CreateIndex(
                name: "IX_Osu.Bind_OsuUserId",
                table: "Osu.Bind",
                column: "OsuUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Osu.Bind_UserId",
                table: "Osu.Bind",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Osu.UserHistory_OsuUserId",
                table: "Osu.UserHistory",
                column: "OsuUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Osu.UserHistory_OsuUserName",
                table: "Osu.UserHistory",
                column: "OsuUserName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Arcaea.Guess");

            migrationBuilder.DropTable(
                name: "BlackList");

            migrationBuilder.DropTable(
                name: "Chunithm.Bind");

            migrationBuilder.DropTable(
                name: "Chunithm.Guess");

            migrationBuilder.DropTable(
                name: "CommandFilter");

            migrationBuilder.DropTable(
                name: "MaiMaiDx.Bind");

            migrationBuilder.DropTable(
                name: "MaiMaiDx.Guess");

            migrationBuilder.DropTable(
                name: "Ongeki.Guess");

            migrationBuilder.DropTable(
                name: "Osu.Bind");

            migrationBuilder.DropTable(
                name: "Osu.UserHistory");
        }
    }
}
