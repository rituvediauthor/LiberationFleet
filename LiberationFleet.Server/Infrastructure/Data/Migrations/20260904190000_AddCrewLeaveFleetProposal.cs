using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddCrewLeaveFleetProposal : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProposalCrewLeaveFleets",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ProposalId = table.Column<int>(type: "int", nullable: false),
                FleetId = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                IsApplied = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProposalCrewLeaveFleets", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProposalCrewLeaveFleets_Fleets_FleetId",
                    column: x => x.FleetId,
                    principalTable: "Fleets",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProposalCrewLeaveFleets_Proposals_ProposalId",
                    column: x => x.ProposalId,
                    principalTable: "Proposals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProposalCrewLeaveFleets_FleetId",
            table: "ProposalCrewLeaveFleets",
            column: "FleetId");

        migrationBuilder.CreateIndex(
            name: "IX_ProposalCrewLeaveFleets_ProposalId",
            table: "ProposalCrewLeaveFleets",
            column: "ProposalId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProposalCrewLeaveFleets");
    }
}
