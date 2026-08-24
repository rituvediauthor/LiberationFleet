using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Normalize AppDonation.Status to int enum and mark Library of Things crew platforms
    /// with a dedicated flag so contribution queries no longer string-match platform names.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260824160000_NormalizeDonationStatusAndLotPlatformFlag")]
    public partial class NormalizeDonationStatusAndLotPlatformFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLibraryOfThings",
                table: "CrewPaymentPlatforms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE [CrewPaymentPlatforms]
                SET [IsLibraryOfThings] = 1
                WHERE [Name] = N'Library of Things';
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_CrewPaymentPlatforms_CrewId_LibraryOfThings'
                      AND object_id = OBJECT_ID(N'[CrewPaymentPlatforms]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_CrewPaymentPlatforms_CrewId_LibraryOfThings]
                    ON [CrewPaymentPlatforms] ([CrewId])
                    WHERE [IsLibraryOfThings] = 1;
                END
                """);

            migrationBuilder.AddColumn<int>(
                name: "StatusInt",
                table: "AppDonations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE [AppDonations]
                SET [StatusInt] = CASE LOWER([Status])
                    WHEN N'completed' THEN 1
                    WHEN N'failed' THEN 2
                    ELSE 0
                END;
                """);

            migrationBuilder.DropIndex(
                name: "IX_AppDonations_UserId_Status_CompletedAt",
                table: "AppDonations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppDonations");

            migrationBuilder.RenameColumn(
                name: "StatusInt",
                table: "AppDonations",
                newName: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppDonations_UserId_Status_CompletedAt",
                table: "AppDonations",
                columns: new[] { "UserId", "Status", "CompletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppDonations_UserId_Status_CompletedAt",
                table: "AppDonations");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "AppDonations",
                newName: "StatusInt");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AppDonations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.Sql("""
                UPDATE [AppDonations]
                SET [Status] = CASE [StatusInt]
                    WHEN 1 THEN N'completed'
                    WHEN 2 THEN N'failed'
                    ELSE N'pending'
                END;
                """);

            migrationBuilder.DropColumn(
                name: "StatusInt",
                table: "AppDonations");

            migrationBuilder.CreateIndex(
                name: "IX_AppDonations_UserId_Status_CompletedAt",
                table: "AppDonations",
                columns: new[] { "UserId", "Status", "CompletedAt" });

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_CrewPaymentPlatforms_CrewId_LibraryOfThings'
                      AND object_id = OBJECT_ID(N'[CrewPaymentPlatforms]')
                )
                BEGIN
                    DROP INDEX [IX_CrewPaymentPlatforms_CrewId_LibraryOfThings] ON [CrewPaymentPlatforms];
                END
                """);

            migrationBuilder.DropColumn(
                name: "IsLibraryOfThings",
                table: "CrewPaymentPlatforms");
        }
    }
}
