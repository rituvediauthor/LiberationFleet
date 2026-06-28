using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    public partial class AddProposalAnonymousAliases : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProposalAnonymousAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProposalId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Nickname = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalAnonymousAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalAnonymousAliases_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProposalAnonymousAliases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProposalCrewmateKicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProposalId = table.Column<int>(type: "int", nullable: false),
                    TargetUserId = table.Column<int>(type: "int", nullable: false),
                    SourceProposalId = table.Column<int>(type: "int", nullable: false),
                    SourceCommentId = table.Column<int>(type: "int", nullable: true),
                    AnonymousNickname = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RevealedUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsApplied = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalCrewmateKicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalCrewmateKicks_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAnonymousAliases_ProposalId_Nickname",
                table: "ProposalAnonymousAliases",
                columns: new[] { "ProposalId", "Nickname" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAnonymousAliases_ProposalId_UserId",
                table: "ProposalAnonymousAliases",
                columns: new[] { "ProposalId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAnonymousAliases_UserId",
                table: "ProposalAnonymousAliases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalCrewmateKicks_ProposalId",
                table: "ProposalCrewmateKicks",
                column: "ProposalId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProposalAnonymousAliases");
            migrationBuilder.DropTable(name: "ProposalCrewmateKicks");
        }
    }
}
