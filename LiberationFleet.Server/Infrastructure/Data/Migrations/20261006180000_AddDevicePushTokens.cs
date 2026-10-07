using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// FCM/APNs device tokens. Snapshot updated so MigrateAsync does not fail with PendingModelChangesWarning.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261006180000_AddDevicePushTokens")]
    public partial class AddDevicePushTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[DevicePushTokens]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [DevicePushTokens] (
                        [Id] int NOT NULL IDENTITY,
                        [UserId] int NOT NULL,
                        [Platform] int NOT NULL,
                        [Token] nvarchar(512) NOT NULL,
                        [DeviceId] nvarchar(128) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [LastSeenAt] datetime2 NOT NULL,
                        [IsDisabled] bit NOT NULL,
                        CONSTRAINT [PK_DevicePushTokens] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_DevicePushTokens_Users_UserId]
                            FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                    );

                    CREATE UNIQUE INDEX [IX_DevicePushTokens_Token]
                        ON [DevicePushTokens] ([Token]);

                    CREATE INDEX [IX_DevicePushTokens_UserId_DeviceId]
                        ON [DevicePushTokens] ([UserId], [DeviceId]);

                    CREATE INDEX [IX_DevicePushTokens_UserId_IsDisabled]
                        ON [DevicePushTokens] ([UserId], [IsDisabled]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[DevicePushTokens]', N'U') IS NOT NULL
                    DROP TABLE [DevicePushTokens];
                """);
        }
    }
}
