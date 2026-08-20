using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260820200000_AddIntermediaryFailureMonthAndCustomGiftCategory")]
    public partial class AddIntermediaryFailureMonthAndCustomGiftCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntermediaryFailureMonthKey",
                table: "CrewMemberships",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IntermediaryFailuresInMonth",
                table: "CrewMemberships",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CustomGiftCategory",
                table: "Gifts",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntermediaryFailureMonthKey",
                table: "CrewMemberships");

            migrationBuilder.DropColumn(
                name: "IntermediaryFailuresInMonth",
                table: "CrewMemberships");

            migrationBuilder.DropColumn(
                name: "CustomGiftCategory",
                table: "Gifts");
        }
    }
}
