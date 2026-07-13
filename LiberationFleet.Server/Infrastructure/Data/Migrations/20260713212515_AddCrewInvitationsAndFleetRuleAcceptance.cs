using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrewInvitationsAndFleetRuleAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InitiatedByFleetInvite",
                table: "ProposalCrewApplyToFleets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CrewInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrewId = table.Column<int>(type: "int", nullable: false),
                    InviterUserId = table.Column<int>(type: "int", nullable: false),
                    InviteeUserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrewInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrewInvitations_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrewInvitations_Users_InviteeUserId",
                        column: x => x.InviteeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CrewInvitations_Users_InviterUserId",
                        column: x => x.InviterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserFleetRuleAcceptances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FleetId = table.Column<int>(type: "int", nullable: false),
                    AcceptedRuleIdsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFleetRuleAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFleetRuleAcceptances_Fleets_FleetId",
                        column: x => x.FleetId,
                        principalTable: "Fleets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserFleetRuleAcceptances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrewInvitations_CrewId_InviteeUserId_Status",
                table: "CrewInvitations",
                columns: new[] { "CrewId", "InviteeUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CrewInvitations_InviteeUserId_Status",
                table: "CrewInvitations",
                columns: new[] { "InviteeUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CrewInvitations_InviterUserId",
                table: "CrewInvitations",
                column: "InviterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFleetRuleAcceptances_FleetId",
                table: "UserFleetRuleAcceptances",
                column: "FleetId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFleetRuleAcceptances_UserId_FleetId",
                table: "UserFleetRuleAcceptances",
                columns: new[] { "UserId", "FleetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrewInvitations");

            migrationBuilder.DropTable(
                name: "UserFleetRuleAcceptances");

            migrationBuilder.DropColumn(
                name: "InitiatedByFleetInvite",
                table: "ProposalCrewApplyToFleets");
        }
    }
}
