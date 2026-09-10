using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260910190000_EncryptUserLocation")]
    public partial class EncryptUserLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "LocationCiphertext",
                table: "Users",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocationKeyVersion",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationNonce",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationCiphertext",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationKeyVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationNonce",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Users",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "Users",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }
    }
}
