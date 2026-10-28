using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimedMutesAndLibraryFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MutedUntil",
                table: "UserMutedContents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserLibraryFavorites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LibraryUnitId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLibraryFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLibraryFavorites_LibraryUnits_LibraryUnitId",
                        column: x => x.LibraryUnitId,
                        principalTable: "LibraryUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLibraryFavorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryFavorites_LibraryUnitId",
                table: "UserLibraryFavorites",
                column: "LibraryUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryFavorites_UserId_LibraryUnitId",
                table: "UserLibraryFavorites",
                columns: new[] { "UserId", "LibraryUnitId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLibraryFavorites");

            migrationBuilder.DropColumn(
                name: "MutedUntil",
                table: "UserMutedContents");
        }
    }
}
