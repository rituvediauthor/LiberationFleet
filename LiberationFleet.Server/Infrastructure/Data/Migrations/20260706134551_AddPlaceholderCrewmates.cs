using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceholderCrewmates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUnclaimedPlaceholder",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlaceholderMember",
                table: "CrewMemberships",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProposalClaimPlaceholderIdentities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProposalId = table.Column<int>(type: "int", nullable: false),
                    PlaceholderUserId = table.Column<int>(type: "int", nullable: false),
                    ClaimantUserId = table.Column<int>(type: "int", nullable: false),
                    PlaceholderDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsApplied = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalClaimPlaceholderIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalClaimPlaceholderIdentities_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalClaimPlaceholderIdentities_ProposalId",
                table: "ProposalClaimPlaceholderIdentities",
                column: "ProposalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalClaimPlaceholderIdentities");

            migrationBuilder.DropColumn(
                name: "IsUnclaimedPlaceholder",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsPlaceholderMember",
                table: "CrewMemberships");
        }
    }
}
