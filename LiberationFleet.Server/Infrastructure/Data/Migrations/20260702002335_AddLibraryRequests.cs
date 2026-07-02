using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LibraryRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    RequesterUserId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    NeededByStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NeededByEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PurposePreview = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HasEncryptedContent = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DeniedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LibraryRequests_LibraryUnits_UnitId",
                        column: x => x.UnitId,
                        principalTable: "LibraryUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryRequests_Users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryRequests_RequesterUserId",
                table: "LibraryRequests",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryRequests_UnitId_RequesterUserId_Status",
                table: "LibraryRequests",
                columns: new[] { "UnitId", "RequesterUserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LibraryRequests");
        }
    }
}
