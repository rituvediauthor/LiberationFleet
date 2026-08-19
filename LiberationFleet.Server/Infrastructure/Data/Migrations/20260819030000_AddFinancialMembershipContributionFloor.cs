using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260819030000_AddFinancialMembershipContributionFloor")]
    public partial class AddFinancialMembershipContributionFloor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FinancialMembershipContributionFloor",
                table: "Crews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinancialMembershipContributionFloor",
                table: "Crews");
        }
    }
}
