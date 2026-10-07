using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

/// <summary>
/// Allow guest donations: nullable UserId + ReceiptEmail. Snapshot updated for PendingModelChangesWarning.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261007190000_AppDonationGuestReceiptEmail")]
public partial class AppDonationGuestReceiptEmail : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Separate batches: SQL Server compiles the whole batch before ALTER takes effect,
        // so ADD + UPDATE in one Sql() fails with "Invalid column name 'ReceiptEmail'".
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'AppDonations', N'ReceiptEmail') IS NULL
            BEGIN
                ALTER TABLE [AppDonations] ADD [ReceiptEmail] nvarchar(256) NOT NULL
                    CONSTRAINT [DF_AppDonations_ReceiptEmail] DEFAULT (N'');
            END
            """);

        migrationBuilder.Sql("""
            UPDATE d
            SET d.[ReceiptEmail] = COALESCE(NULLIF(LTRIM(RTRIM(u.[Email])), N''), N'unknown@invalid.local')
            FROM [AppDonations] d
            INNER JOIN [Users] u ON u.[Id] = d.[UserId]
            WHERE d.[ReceiptEmail] = N'' OR d.[ReceiptEmail] IS NULL;
            """);

        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = N'FK_AppDonations_Users_UserId' AND parent_object_id = OBJECT_ID(N'AppDonations'))
            BEGIN
                ALTER TABLE [AppDonations] DROP CONSTRAINT [FK_AppDonations_Users_UserId];
            END

            ALTER TABLE [AppDonations] ALTER COLUMN [UserId] int NULL;

            IF NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = N'FK_AppDonations_Users_UserId' AND parent_object_id = OBJECT_ID(N'AppDonations'))
            BEGIN
                ALTER TABLE [AppDonations] WITH CHECK ADD CONSTRAINT [FK_AppDonations_Users_UserId]
                    FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM [AppDonations] WHERE [UserId] IS NULL;

            IF EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = N'FK_AppDonations_Users_UserId' AND parent_object_id = OBJECT_ID(N'AppDonations'))
            BEGIN
                ALTER TABLE [AppDonations] DROP CONSTRAINT [FK_AppDonations_Users_UserId];
            END

            ALTER TABLE [AppDonations] ALTER COLUMN [UserId] int NOT NULL;

            ALTER TABLE [AppDonations] WITH CHECK ADD CONSTRAINT [FK_AppDonations_Users_UserId]
                FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE;
            """);

        migrationBuilder.Sql("""
            IF COL_LENGTH(N'AppDonations', N'ReceiptEmail') IS NOT NULL
            BEGIN
                ALTER TABLE [AppDonations] DROP CONSTRAINT [DF_AppDonations_ReceiptEmail];
                ALTER TABLE [AppDonations] DROP COLUMN [ReceiptEmail];
            END
            """);
    }
}
