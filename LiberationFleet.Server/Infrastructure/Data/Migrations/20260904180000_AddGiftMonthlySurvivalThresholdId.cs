using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260904180000_AddGiftMonthlySurvivalThresholdId")]
    public partial class AddGiftMonthlySurvivalThresholdId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MonthlySurvivalThresholdId",
                table: "Gifts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_MonthlySurvivalThresholdId",
                table: "Gifts",
                column: "MonthlySurvivalThresholdId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gifts_MonthlySurvivalThresholds_MonthlySurvivalThresholdId",
                table: "Gifts",
                column: "MonthlySurvivalThresholdId",
                principalTable: "MonthlySurvivalThresholds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gifts_MonthlySurvivalThresholds_MonthlySurvivalThresholdId",
                table: "Gifts");

            migrationBuilder.DropIndex(
                name: "IX_Gifts_MonthlySurvivalThresholdId",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "MonthlySurvivalThresholdId",
                table: "Gifts");
        }
    }
}
