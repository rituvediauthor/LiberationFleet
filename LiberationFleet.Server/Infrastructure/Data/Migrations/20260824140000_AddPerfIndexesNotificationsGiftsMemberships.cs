using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Indexes for notification badge scans, gift contribution aggregates, and season membership filters.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260824140000_AddPerfIndexesNotificationsGiftsMemberships")]
    public partial class AddPerfIndexesNotificationsGiftsMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Notifications_UserId_IsRead_Kind'
                      AND object_id = OBJECT_ID(N'[Notifications]')
                )
                BEGIN
                    CREATE INDEX [IX_Notifications_UserId_IsRead_Kind]
                    ON [Notifications] ([UserId], [IsRead], [Kind]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Gifts_CrewId_GiverUserId_CreatedAt'
                      AND object_id = OBJECT_ID(N'[Gifts]')
                )
                BEGIN
                    CREATE INDEX [IX_Gifts_CrewId_GiverUserId_CreatedAt]
                    ON [Gifts] ([CrewId], [GiverUserId], [CreatedAt]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_CrewMemberships_CrewId_IsBanned_IsInSeason'
                      AND object_id = OBJECT_ID(N'[CrewMemberships]')
                )
                BEGIN
                    CREATE INDEX [IX_CrewMemberships_CrewId_IsBanned_IsInSeason]
                    ON [CrewMemberships] ([CrewId], [IsBanned], [IsInSeason]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_CrewMemberships_CrewId_IsBanned_IsSeasonReady'
                      AND object_id = OBJECT_ID(N'[CrewMemberships]')
                )
                BEGIN
                    CREATE INDEX [IX_CrewMemberships_CrewId_IsBanned_IsSeasonReady]
                    ON [CrewMemberships] ([CrewId], [IsBanned], [IsSeasonReady]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Notifications_UserId_IsRead_Kind'
                      AND object_id = OBJECT_ID(N'[Notifications]')
                )
                BEGIN
                    DROP INDEX [IX_Notifications_UserId_IsRead_Kind] ON [Notifications];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Gifts_CrewId_GiverUserId_CreatedAt'
                      AND object_id = OBJECT_ID(N'[Gifts]')
                )
                BEGIN
                    DROP INDEX [IX_Gifts_CrewId_GiverUserId_CreatedAt] ON [Gifts];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_CrewMemberships_CrewId_IsBanned_IsInSeason'
                      AND object_id = OBJECT_ID(N'[CrewMemberships]')
                )
                BEGIN
                    DROP INDEX [IX_CrewMemberships_CrewId_IsBanned_IsInSeason] ON [CrewMemberships];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_CrewMemberships_CrewId_IsBanned_IsSeasonReady'
                      AND object_id = OBJECT_ID(N'[CrewMemberships]')
                )
                BEGIN
                    DROP INDEX [IX_CrewMemberships_CrewId_IsBanned_IsSeasonReady] ON [CrewMemberships];
                END
                """);
        }
    }
}
