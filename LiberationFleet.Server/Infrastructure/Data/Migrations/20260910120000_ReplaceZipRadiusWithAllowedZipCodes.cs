using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260910120000_ReplaceZipRadiusWithAllowedZipCodes")]
    public partial class ReplaceZipRadiusWithAllowedZipCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "Users",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CrewAllowedZipCodes",
                columns: table => new
                {
                    CrewId = table.Column<int>(type: "int", nullable: false),
                    ZipCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrewAllowedZipCodes", x => new { x.CrewId, x.ZipCode });
                    table.ForeignKey(
                        name: "FK_CrewAllowedZipCodes_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FleetAllowedZipCodes",
                columns: table => new
                {
                    FleetId = table.Column<int>(type: "int", nullable: false),
                    ZipCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetAllowedZipCodes", x => new { x.FleetId, x.ZipCode });
                    table.ForeignKey(
                        name: "FK_FleetAllowedZipCodes_Fleets_FleetId",
                        column: x => x.FleetId,
                        principalTable: "Fleets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LibraryOfferingAllowedZipCodes",
                columns: table => new
                {
                    OfferingId = table.Column<int>(type: "int", nullable: false),
                    ZipCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryOfferingAllowedZipCodes", x => new { x.OfferingId, x.ZipCode });
                    table.ForeignKey(
                        name: "FK_LibraryOfferingAllowedZipCodes_LibraryOfferings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "LibraryOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrewAllowedZipCodes_ZipCode",
                table: "CrewAllowedZipCodes",
                column: "ZipCode");

            migrationBuilder.CreateIndex(
                name: "IX_FleetAllowedZipCodes_ZipCode",
                table: "FleetAllowedZipCodes",
                column: "ZipCode");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryOfferingAllowedZipCodes_ZipCode",
                table: "LibraryOfferingAllowedZipCodes",
                column: "ZipCode");

            // Preserve existing Local center zips as single-entry allowlists.
            migrationBuilder.Sql("""
                INSERT INTO CrewAllowedZipCodes (CrewId, ZipCode)
                SELECT Id, ZipCode
                FROM Crews
                WHERE ZipCode IS NOT NULL AND LEN(LTRIM(RTRIM(ZipCode))) > 0;
                """);

            migrationBuilder.Sql("""
                INSERT INTO FleetAllowedZipCodes (FleetId, ZipCode)
                SELECT Id, ZipCode
                FROM Fleets
                WHERE ZipCode IS NOT NULL AND LEN(LTRIM(RTRIM(ZipCode))) > 0;
                """);

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "RadiusMiles",
                table: "Crews");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "Fleets");

            migrationBuilder.DropColumn(
                name: "RadiusMiles",
                table: "Fleets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "Crews",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RadiusMiles",
                table: "Crews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "Fleets",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RadiusMiles",
                table: "Fleets",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE c
                SET c.ZipCode = z.ZipCode,
                    c.RadiusMiles = 25
                FROM Crews c
                CROSS APPLY (
                    SELECT TOP 1 ZipCode
                    FROM CrewAllowedZipCodes
                    WHERE CrewId = c.Id
                    ORDER BY ZipCode
                ) z;
                """);

            migrationBuilder.Sql("""
                UPDATE f
                SET f.ZipCode = z.ZipCode,
                    f.RadiusMiles = 25
                FROM Fleets f
                CROSS APPLY (
                    SELECT TOP 1 ZipCode
                    FROM FleetAllowedZipCodes
                    WHERE FleetId = f.Id
                    ORDER BY ZipCode
                ) z;
                """);

            migrationBuilder.DropTable(name: "CrewAllowedZipCodes");
            migrationBuilder.DropTable(name: "FleetAllowedZipCodes");
            migrationBuilder.DropTable(name: "LibraryOfferingAllowedZipCodes");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "Users");
        }
    }
}
