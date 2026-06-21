using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Gifts', 'CycleAmountApplied') IS NOT NULL
                    ALTER TABLE [Gifts] DROP COLUMN [CycleAmountApplied];

                IF COL_LENGTH('Gifts', 'SeasonStartDateApplied') IS NOT NULL
                    ALTER TABLE [Gifts] DROP COLUMN [SeasonStartDateApplied];
                """);

            migrationBuilder.AddColumn<bool>(
                name: "CountsTowardContribution",
                table: "Gifts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceptionApplied",
                table: "Gifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VerificationStatus",
                table: "Gifts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE Gifts SET VerificationStatus = 6, ReceptionApplied = 1 WHERE Type IN (0, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountsTowardContribution",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "ReceptionApplied",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "Gifts");
        }
    }
}
