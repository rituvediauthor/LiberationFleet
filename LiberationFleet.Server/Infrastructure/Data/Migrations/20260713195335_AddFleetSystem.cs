using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CrewId",
                table: "Proposals",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "FleetId",
                table: "Proposals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowCrossCrewGiving",
                table: "Crews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "CrewId",
                table: "ChatRooms",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "FleetId",
                table: "ChatRooms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkedCrewId",
                table: "ChatRooms",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Fleets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Privacy = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    ZipCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RadiusMiles = table.Column<int>(type: "int", nullable: true),
                    JoinCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequireApprovalForEdits = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LibraryOfThingsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AllowCrewmateFileAttachments = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MinimumCrewmateTenureDaysForAttachments = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MinimumContributionForAttachments = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    MinimumCrewmateTenureDaysForProposals = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MinimumContributionForProposals = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fleets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fleets_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalFleetKickCrews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProposalId = table.Column<int>(type: "int", nullable: false),
                    TargetCrewId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsApplied = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalFleetKickCrews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalFleetKickCrews_Crews_TargetCrewId",
                        column: x => x.TargetCrewId,
                        principalTable: "Crews",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProposalFleetKickCrews_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProposalFleetSettingChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProposalId = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<int>(type: "int", nullable: false),
                    NewValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsApplied = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalFleetSettingChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalFleetSettingChanges_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FleetCrews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FleetId = table.Column<int>(type: "int", nullable: false),
                    CrewId = table.Column<int>(type: "int", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetCrews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetCrews_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetCrews_Fleets_FleetId",
                        column: x => x.FleetId,
                        principalTable: "Fleets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FleetRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FleetId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetRules_Fleets_FleetId",
                        column: x => x.FleetId,
                        principalTable: "Fleets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetRules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProposalCrewApplyToFleets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProposalId = table.Column<int>(type: "int", nullable: false),
                    FleetId = table.Column<int>(type: "int", nullable: false),
                    TargetJoinCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    AcceptedRuleIdsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsApplied = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalCrewApplyToFleets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalCrewApplyToFleets_Fleets_FleetId",
                        column: x => x.FleetId,
                        principalTable: "Fleets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProposalCrewApplyToFleets_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProposalFleetJoinRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProposalId = table.Column<int>(type: "int", nullable: false),
                    FleetId = table.Column<int>(type: "int", nullable: false),
                    ApplicantCrewId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsApplied = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalFleetJoinRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalFleetJoinRequests_Crews_ApplicantCrewId",
                        column: x => x.ApplicantCrewId,
                        principalTable: "Crews",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProposalFleetJoinRequests_Fleets_FleetId",
                        column: x => x.FleetId,
                        principalTable: "Fleets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProposalFleetJoinRequests_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_FleetId",
                table: "Proposals",
                column: "FleetId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Proposals_CrewOrFleet",
                table: "Proposals",
                sql: "[CrewId] IS NOT NULL OR [FleetId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_FleetId",
                table: "ChatRooms",
                column: "FleetId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_LinkedCrewId",
                table: "ChatRooms",
                column: "LinkedCrewId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatRooms_CrewOrFleet",
                table: "ChatRooms",
                sql: "[CrewId] IS NOT NULL OR [FleetId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCrews_CrewId",
                table: "FleetCrews",
                column: "CrewId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FleetCrews_FleetId_CrewId",
                table: "FleetCrews",
                columns: new[] { "FleetId", "CrewId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FleetRules_CreatedByUserId",
                table: "FleetRules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetRules_FleetId",
                table: "FleetRules",
                column: "FleetId");

            migrationBuilder.CreateIndex(
                name: "IX_Fleets_CreatedByUserId",
                table: "Fleets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Fleets_JoinCode",
                table: "Fleets",
                column: "JoinCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalCrewApplyToFleets_FleetId",
                table: "ProposalCrewApplyToFleets",
                column: "FleetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalCrewApplyToFleets_ProposalId",
                table: "ProposalCrewApplyToFleets",
                column: "ProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalFleetJoinRequests_ApplicantCrewId",
                table: "ProposalFleetJoinRequests",
                column: "ApplicantCrewId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalFleetJoinRequests_FleetId",
                table: "ProposalFleetJoinRequests",
                column: "FleetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalFleetJoinRequests_ProposalId",
                table: "ProposalFleetJoinRequests",
                column: "ProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalFleetKickCrews_ProposalId",
                table: "ProposalFleetKickCrews",
                column: "ProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalFleetKickCrews_TargetCrewId",
                table: "ProposalFleetKickCrews",
                column: "TargetCrewId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalFleetSettingChanges_ProposalId",
                table: "ProposalFleetSettingChanges",
                column: "ProposalId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_Crews_LinkedCrewId",
                table: "ChatRooms",
                column: "LinkedCrewId",
                principalTable: "Crews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_Fleets_FleetId",
                table: "ChatRooms",
                column: "FleetId",
                principalTable: "Fleets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Proposals_Fleets_FleetId",
                table: "Proposals",
                column: "FleetId",
                principalTable: "Fleets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_Crews_LinkedCrewId",
                table: "ChatRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_Fleets_FleetId",
                table: "ChatRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_Proposals_Fleets_FleetId",
                table: "Proposals");

            migrationBuilder.DropTable(
                name: "FleetCrews");

            migrationBuilder.DropTable(
                name: "FleetRules");

            migrationBuilder.DropTable(
                name: "ProposalCrewApplyToFleets");

            migrationBuilder.DropTable(
                name: "ProposalFleetJoinRequests");

            migrationBuilder.DropTable(
                name: "ProposalFleetKickCrews");

            migrationBuilder.DropTable(
                name: "ProposalFleetSettingChanges");

            migrationBuilder.DropTable(
                name: "Fleets");

            migrationBuilder.DropIndex(
                name: "IX_Proposals_FleetId",
                table: "Proposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Proposals_CrewOrFleet",
                table: "Proposals");

            migrationBuilder.DropIndex(
                name: "IX_ChatRooms_FleetId",
                table: "ChatRooms");

            migrationBuilder.DropIndex(
                name: "IX_ChatRooms_LinkedCrewId",
                table: "ChatRooms");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatRooms_CrewOrFleet",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "FleetId",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "AllowCrossCrewGiving",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "FleetId",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "LinkedCrewId",
                table: "ChatRooms");

            migrationBuilder.AlterColumn<int>(
                name: "CrewId",
                table: "Proposals",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CrewId",
                table: "ChatRooms",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
