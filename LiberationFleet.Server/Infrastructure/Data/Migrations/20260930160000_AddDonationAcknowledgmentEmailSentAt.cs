using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260930160000_AddDonationAcknowledgmentEmailSentAt")]
public partial class AddDonationAcknowledgmentEmailSentAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'AppDonations', N'AcknowledgmentEmailSentAt') IS NULL
            BEGIN
                ALTER TABLE [AppDonations] ADD [AcknowledgmentEmailSentAt] datetime2 NULL;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'AppDonations', N'AcknowledgmentEmailSentAt') IS NOT NULL
            BEGIN
                ALTER TABLE [AppDonations] DROP COLUMN [AcknowledgmentEmailSentAt];
            END
            """);
    }
}
