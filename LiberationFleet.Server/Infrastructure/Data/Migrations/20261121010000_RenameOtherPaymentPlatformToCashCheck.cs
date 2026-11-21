using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

/// <summary>
/// Renames the global "Other" payment platform to "Cash/check" (sort first) and
/// remaps crew/user platform names so in-person aid is the clear default.
/// Uses SQL (not UpdateData) so EF does not require a mapped entity for the seed row.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261121010000_RenameOtherPaymentPlatformToCashCheck")]
public partial class RenameOtherPaymentPlatformToCashCheck : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [PaymentPlatforms]
            SET [Name] = N'Cash/check', [SortOrder] = 0
            WHERE [Id] = 5;
            """);

        // Prefer an existing Cash/check crew platform when both Other and Cash/check exist.
        migrationBuilder.Sql("""
            UPDATE upp
            SET upp.CrewPaymentPlatformId = cash.Id,
                upp.PlatformName = N'Cash/check'
            FROM UserPaymentPlatforms upp
            INNER JOIN CrewPaymentPlatforms other
                ON other.Id = upp.CrewPaymentPlatformId
               AND other.Name = N'Other'
            INNER JOIN CrewPaymentPlatforms cash
                ON cash.CrewId = other.CrewId
               AND cash.Name = N'Cash/check'
               AND cash.IsLibraryOfThings = 0;
            """);

        migrationBuilder.Sql("""
            DELETE other
            FROM CrewPaymentPlatforms other
            WHERE other.Name = N'Other'
              AND EXISTS (
                  SELECT 1
                  FROM CrewPaymentPlatforms cash
                  WHERE cash.CrewId = other.CrewId
                    AND cash.Name = N'Cash/check'
                    AND cash.IsLibraryOfThings = 0
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM UserPaymentPlatforms upp
                  WHERE upp.CrewPaymentPlatformId = other.Id
              );
            """);

        migrationBuilder.Sql("""
            UPDATE CrewPaymentPlatforms
            SET Name = N'Cash/check'
            WHERE Name = N'Other';
            """);

        migrationBuilder.Sql("""
            UPDATE UserPaymentPlatforms
            SET PlatformName = N'Cash/check'
            WHERE PlatformName = N'Other';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [PaymentPlatforms]
            SET [Name] = N'Other', [SortOrder] = 5
            WHERE [Id] = 5;
            """);

        migrationBuilder.Sql("""
            UPDATE CrewPaymentPlatforms
            SET Name = N'Other'
            WHERE Name = N'Cash/check'
              AND IsLibraryOfThings = 0;
            """);

        migrationBuilder.Sql("""
            UPDATE UserPaymentPlatforms
            SET PlatformName = N'Other'
            WHERE PlatformName = N'Cash/check';
            """);
    }
}
