using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetRuleChangeProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlaintextName",
                table: "ProposalCrewChatChanges",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "ChatRoomMessages",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProposalFleetRuleChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProposalId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    RuleId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RuleTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RuleDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsApplied = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalFleetRuleChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalFleetRuleChanges_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalFleetRuleChanges_ProposalId",
                table: "ProposalFleetRuleChanges",
                column: "ProposalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalFleetRuleChanges");

            migrationBuilder.DropColumn(
                name: "PlaintextName",
                table: "ProposalCrewChatChanges");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "ChatRoomMessages");
        }
    }
}
