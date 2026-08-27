using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

/// <summary>
/// Two-voter proposal timeout mode on crews and fleets.
/// Hand-written (no Designer); attributes required so MigrateAsync discovers it.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260825190000_AddDuoVoteTimeoutMode")]
public partial class AddDuoVoteTimeoutMode : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent: snapshot/history can drift ahead of live Docker/local SQL.
        migrationBuilder.Sql("""
            IF COL_LENGTH('Crews', 'DuoVoteTimeoutMode') IS NULL
            BEGIN
                ALTER TABLE [Crews] ADD [DuoVoteTimeoutMode] int NOT NULL
                    CONSTRAINT [DF_Crews_DuoVoteTimeoutMode] DEFAULT 1;
            END

            IF COL_LENGTH('Fleets', 'DuoVoteTimeoutMode') IS NULL
            BEGIN
                ALTER TABLE [Fleets] ADD [DuoVoteTimeoutMode] int NOT NULL
                    CONSTRAINT [DF_Fleets_DuoVoteTimeoutMode] DEFAULT 1;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('Crews', 'DuoVoteTimeoutMode') IS NOT NULL
            BEGIN
                DECLARE @crewDf sysname;
                SELECT @crewDf = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[Crews]') AND c.name = N'DuoVoteTimeoutMode';
                IF @crewDf IS NOT NULL
                    EXEC(N'ALTER TABLE [Crews] DROP CONSTRAINT [' + @crewDf + N']');
                ALTER TABLE [Crews] DROP COLUMN [DuoVoteTimeoutMode];
            END

            IF COL_LENGTH('Fleets', 'DuoVoteTimeoutMode') IS NOT NULL
            BEGIN
                DECLARE @fleetDf sysname;
                SELECT @fleetDf = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[Fleets]') AND c.name = N'DuoVoteTimeoutMode';
                IF @fleetDf IS NOT NULL
                    EXEC(N'ALTER TABLE [Fleets] DROP CONSTRAINT [' + @fleetDf + N']');
                ALTER TABLE [Fleets] DROP COLUMN [DuoVoteTimeoutMode];
            END
            """);
    }
}
