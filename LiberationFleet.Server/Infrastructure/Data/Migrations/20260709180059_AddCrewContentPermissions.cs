using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrewContentPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowCrewmateFileAttachments",
                table: "Crews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumContributionForAttachments",
                table: "Crews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumContributionForProposals",
                table: "Crews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MinimumCrewmateTenureDaysForAttachments",
                table: "Crews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumCrewmateTenureDaysForProposals",
                table: "Crews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<bool>(
                name: "CanAttachFiles",
                table: "CrewMemberships",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanCreateProposals",
                table: "CrewMemberships",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProposalCrewmatePermissionGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProposalId = table.Column<int>(type: "int", nullable: false),
                    TargetUserId = table.Column<int>(type: "int", nullable: false),
                    GrantType = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsApplied = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalCrewmatePermissionGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalCrewmatePermissionGrants_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalCrewmatePermissionGrants_ProposalId",
                table: "ProposalCrewmatePermissionGrants",
                column: "ProposalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalCrewmatePermissionGrants");

            migrationBuilder.DropColumn(
                name: "AllowCrewmateFileAttachments",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "MinimumContributionForAttachments",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "MinimumContributionForProposals",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "MinimumCrewmateTenureDaysForAttachments",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "MinimumCrewmateTenureDaysForProposals",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "CanCreateProposals",
                table: "CrewMemberships");

            migrationBuilder.AlterColumn<bool>(
                name: "CanAttachFiles",
                table: "CrewMemberships",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);
        }
    }
}
