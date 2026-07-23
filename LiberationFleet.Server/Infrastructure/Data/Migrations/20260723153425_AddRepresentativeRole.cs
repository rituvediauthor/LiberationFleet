using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRepresentativeRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RepresentativeTermEndUtc",
                table: "ProposalCrewRoleChanges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RepresentativeTermStartUtc",
                table: "ProposalCrewRoleChanges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRepresentativeGift",
                table: "Gifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRepresentative",
                table: "CrewMemberships",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RepresentativeReceivedAmount",
                table: "CrewMemberships",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "RepresentativeTermEndUtc",
                table: "CrewMemberships",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RepresentativeTermStartUtc",
                table: "CrewMemberships",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepresentativeTermEndUtc",
                table: "ProposalCrewRoleChanges");

            migrationBuilder.DropColumn(
                name: "RepresentativeTermStartUtc",
                table: "ProposalCrewRoleChanges");

            migrationBuilder.DropColumn(
                name: "IsRepresentativeGift",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "IsRepresentative",
                table: "CrewMemberships");

            migrationBuilder.DropColumn(
                name: "RepresentativeReceivedAmount",
                table: "CrewMemberships");

            migrationBuilder.DropColumn(
                name: "RepresentativeTermEndUtc",
                table: "CrewMemberships");

            migrationBuilder.DropColumn(
                name: "RepresentativeTermStartUtc",
                table: "CrewMemberships");
        }
    }
}
