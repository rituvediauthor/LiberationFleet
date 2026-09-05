using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260905200000_PortableUserPaymentPlatforms")]
public partial class PortableUserPaymentPlatforms : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PlatformName",
            table: "UserPaymentPlatforms",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("""
            UPDATE upp
            SET upp.PlatformName = COALESCE(NULLIF(LTRIM(RTRIM(cpp.Name)), ''), 'Unknown')
            FROM UserPaymentPlatforms upp
            INNER JOIN CrewPaymentPlatforms cpp ON cpp.Id = upp.CrewPaymentPlatformId
            """);

        migrationBuilder.AlterColumn<int>(
            name: "CrewPaymentPlatformId",
            table: "UserPaymentPlatforms",
            type: "int",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "int");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM UserPaymentPlatforms WHERE CrewPaymentPlatformId IS NULL
            """);

        migrationBuilder.AlterColumn<int>(
            name: "CrewPaymentPlatformId",
            table: "UserPaymentPlatforms",
            type: "int",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "PlatformName",
            table: "UserPaymentPlatforms");
    }
}
