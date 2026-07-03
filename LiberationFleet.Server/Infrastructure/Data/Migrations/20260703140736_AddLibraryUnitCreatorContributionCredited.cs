using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryUnitCreatorContributionCredited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CreatorContributionCredited",
                table: "LibraryUnits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE lu
                SET CreatorContributionCredited = 1
                FROM LibraryUnits lu
                INNER JOIN LibraryOfferings lo ON lu.OfferingId = lo.Id
                WHERE lo.Kind = 0
                  AND lu.CurrentPossessorUserId <> lo.CreatorUserId
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatorContributionCredited",
                table: "LibraryUnits");
        }
    }
}
