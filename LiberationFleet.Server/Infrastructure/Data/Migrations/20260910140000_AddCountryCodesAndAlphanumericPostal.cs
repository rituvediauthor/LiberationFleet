using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260910140000_AddCountryCodesAndAlphanumericPostal")]
    public partial class AddCountryCodesAndAlphanumericPostal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Users",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                table: "Users",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Crews",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Fleets",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "LibraryOfferings",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                table: "CrewAllowedZipCodes",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                table: "FleetAllowedZipCodes",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                table: "LibraryOfferingAllowedZipCodes",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            // Existing Local groups with postal allowlists default to US.
            migrationBuilder.Sql("""
                UPDATE Crews
                SET CountryCode = 'US'
                WHERE CountryCode IS NULL
                  AND EXISTS (SELECT 1 FROM CrewAllowedZipCodes z WHERE z.CrewId = Crews.Id);
                """);

            migrationBuilder.Sql("""
                UPDATE Fleets
                SET CountryCode = 'US'
                WHERE CountryCode IS NULL
                  AND EXISTS (SELECT 1 FROM FleetAllowedZipCodes z WHERE z.FleetId = Fleets.Id);
                """);

            migrationBuilder.Sql("""
                UPDATE LibraryOfferings
                SET CountryCode = 'US'
                WHERE CountryCode IS NULL
                  AND EXISTS (
                    SELECT 1 FROM LibraryOfferingAllowedZipCodes z
                    WHERE z.OfferingId = LibraryOfferings.Id);
                """);

            migrationBuilder.Sql("""
                UPDATE Users
                SET CountryCode = 'US'
                WHERE CountryCode IS NULL
                  AND ZipCode IS NOT NULL
                  AND LEN(LTRIM(RTRIM(ZipCode))) > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CountryCode", table: "Users");
            migrationBuilder.DropColumn(name: "CountryCode", table: "Crews");
            migrationBuilder.DropColumn(name: "CountryCode", table: "Fleets");
            migrationBuilder.DropColumn(name: "CountryCode", table: "LibraryOfferings");

            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                table: "Users",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                table: "CrewAllowedZipCodes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                table: "FleetAllowedZipCodes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                table: "LibraryOfferingAllowedZipCodes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);
        }
    }
}
