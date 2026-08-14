using System;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260813180000_AddChatChannelOrdering")]
    public partial class AddChatChannelOrdering : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ChatRooms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OrderedRoomIdsJson",
                table: "ProposalCrewChatChanges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserChatChannelOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CrewId = table.Column<int>(type: "int", nullable: true),
                    FleetId = table.Column<int>(type: "int", nullable: true),
                    OrderedRoomIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChatChannelOrders", x => x.Id);
                    table.CheckConstraint(
                        "CK_UserChatChannelOrders_CrewOrFleet",
                        "([CrewId] IS NOT NULL AND [FleetId] IS NULL) OR ([CrewId] IS NULL AND [FleetId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_UserChatChannelOrders_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserChatChannelOrders_Fleets_FleetId",
                        column: x => x.FleetId,
                        principalTable: "Fleets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserChatChannelOrders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserChatChannelOrders_CrewId",
                table: "UserChatChannelOrders",
                column: "CrewId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChatChannelOrders_FleetId",
                table: "UserChatChannelOrders",
                column: "FleetId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChatChannelOrders_UserId_CrewId",
                table: "UserChatChannelOrders",
                columns: new[] { "UserId", "CrewId" },
                unique: true,
                filter: "[CrewId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserChatChannelOrders_UserId_FleetId",
                table: "UserChatChannelOrders",
                columns: new[] { "UserId", "FleetId" },
                unique: true,
                filter: "[FleetId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserChatChannelOrders");
            migrationBuilder.DropColumn(name: "SortOrder", table: "ChatRooms");
            migrationBuilder.DropColumn(name: "OrderedRoomIdsJson", table: "ProposalCrewChatChanges");
        }
    }
}
