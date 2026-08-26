using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddDuoVoteTimeoutMode : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DuoVoteTimeoutMode",
            table: "Crews",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "DuoVoteTimeoutMode",
            table: "Fleets",
            type: "int",
            nullable: false,
            defaultValue: 1);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DuoVoteTimeoutMode",
            table: "Crews");

        migrationBuilder.DropColumn(
            name: "DuoVoteTimeoutMode",
            table: "Fleets");
    }
}
