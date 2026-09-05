using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260905190000_CrewMembershipSoftLeaveAndPercentBonus")]
public partial class CrewMembershipSoftLeaveAndPercentBonus : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "LeftAt",
            table: "CrewMemberships",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PercentBonus",
            table: "CrewMemberships",
            type: "int",
            nullable: false,
            defaultValue: 0);

        // Seed each membership with the user's legacy global boost so current crews keep continuity.
        migrationBuilder.Sql("""
            UPDATE cm
            SET cm.PercentBonus = u.PercentBonus
            FROM CrewMemberships cm
            INNER JOIN Users u ON u.Id = cm.UserId
            """);

        migrationBuilder.CreateIndex(
            name: "IX_CrewMemberships_CrewId_IsBanned_LeftAt",
            table: "CrewMemberships",
            columns: new[] { "CrewId", "IsBanned", "LeftAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CrewMemberships_CrewId_IsBanned_LeftAt",
            table: "CrewMemberships");

        migrationBuilder.DropColumn(
            name: "LeftAt",
            table: "CrewMemberships");

        migrationBuilder.DropColumn(
            name: "PercentBonus",
            table: "CrewMemberships");
    }
}
