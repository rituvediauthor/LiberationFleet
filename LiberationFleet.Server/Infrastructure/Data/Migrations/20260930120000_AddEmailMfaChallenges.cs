using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

/// Email MFA OTP challenges. Schema is created in Up(); snapshot updated so
/// MigrateAsync does not fail with PendingModelChangesWarning.
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260930120000_AddEmailMfaChallenges")]
public partial class AddEmailMfaChallenges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[EmailMfaChallenges]', N'U') IS NULL
            BEGIN
                CREATE TABLE [EmailMfaChallenges] (
                    [Id] int NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [ChallengeToken] nvarchar(128) NOT NULL,
                    [CodeHash] nvarchar(64) NOT NULL,
                    [Purpose] int NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [ExpiresAt] datetime2 NOT NULL,
                    [ConsumedAt] datetime2 NULL,
                    [LastSentAt] datetime2 NOT NULL,
                    [AttemptCount] int NOT NULL CONSTRAINT [DF_EmailMfaChallenges_AttemptCount] DEFAULT 0,
                    [DeviceId] nvarchar(128) NULL,
                    [DeviceName] nvarchar(128) NULL,
                    [UserAgent] nvarchar(512) NULL,
                    CONSTRAINT [PK_EmailMfaChallenges] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_EmailMfaChallenges_Users_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_EmailMfaChallenges_ChallengeToken]
                    ON [EmailMfaChallenges] ([ChallengeToken]);

                CREATE INDEX [IX_EmailMfaChallenges_UserId_Purpose_ConsumedAt]
                    ON [EmailMfaChallenges] ([UserId], [Purpose], [ConsumedAt]);
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[EmailMfaChallenges]', N'U') IS NOT NULL
            BEGIN
                DROP TABLE [EmailMfaChallenges];
            END
            """);
    }
}
