using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

/// <summary>
/// Configurable proposal auto-resolve timers on crews and fleets.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260828180000_AddProposalAutoResolveSettings")]
public partial class AddProposalAutoResolveSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('Crews', 'AutoResolveOverTime') IS NULL
            BEGIN
                ALTER TABLE [Crews] ADD [AutoResolveOverTime] bit NOT NULL
                    CONSTRAINT [DF_Crews_AutoResolveOverTime] DEFAULT 1;
            END

            IF COL_LENGTH('Crews', 'BaseAutoResolveHours') IS NULL
            BEGIN
                ALTER TABLE [Crews] ADD [BaseAutoResolveHours] int NOT NULL
                    CONSTRAINT [DF_Crews_BaseAutoResolveHours] DEFAULT 24;
            END

            IF COL_LENGTH('Crews', 'ChangeAutoResolveTimerOnFirstReject') IS NULL
            BEGIN
                ALTER TABLE [Crews] ADD [ChangeAutoResolveTimerOnFirstReject] bit NOT NULL
                    CONSTRAINT [DF_Crews_ChangeAutoResolveTimerOnFirstReject] DEFAULT 1;
            END

            IF COL_LENGTH('Crews', 'AutoResolveHoursAfterFirstReject') IS NULL
            BEGIN
                ALTER TABLE [Crews] ADD [AutoResolveHoursAfterFirstReject] int NOT NULL
                    CONSTRAINT [DF_Crews_AutoResolveHoursAfterFirstReject] DEFAULT 168;
            END

            IF COL_LENGTH('Fleets', 'AutoResolveOverTime') IS NULL
            BEGIN
                ALTER TABLE [Fleets] ADD [AutoResolveOverTime] bit NOT NULL
                    CONSTRAINT [DF_Fleets_AutoResolveOverTime] DEFAULT 1;
            END

            IF COL_LENGTH('Fleets', 'BaseAutoResolveHours') IS NULL
            BEGIN
                ALTER TABLE [Fleets] ADD [BaseAutoResolveHours] int NOT NULL
                    CONSTRAINT [DF_Fleets_BaseAutoResolveHours] DEFAULT 24;
            END

            IF COL_LENGTH('Fleets', 'ChangeAutoResolveTimerOnFirstReject') IS NULL
            BEGIN
                ALTER TABLE [Fleets] ADD [ChangeAutoResolveTimerOnFirstReject] bit NOT NULL
                    CONSTRAINT [DF_Fleets_ChangeAutoResolveTimerOnFirstReject] DEFAULT 1;
            END

            IF COL_LENGTH('Fleets', 'AutoResolveHoursAfterFirstReject') IS NULL
            BEGIN
                ALTER TABLE [Fleets] ADD [AutoResolveHoursAfterFirstReject] int NOT NULL
                    CONSTRAINT [DF_Fleets_AutoResolveHoursAfterFirstReject] DEFAULT 168;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('Crews', 'AutoResolveOverTime') IS NOT NULL
            BEGIN
                DECLARE @crewAutoResolveDf sysname;
                SELECT @crewAutoResolveDf = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[Crews]') AND c.name = N'AutoResolveOverTime';
                IF @crewAutoResolveDf IS NOT NULL
                    EXEC(N'ALTER TABLE [Crews] DROP CONSTRAINT [' + @crewAutoResolveDf + N']');
                ALTER TABLE [Crews] DROP COLUMN [AutoResolveOverTime];
            END

            IF COL_LENGTH('Crews', 'BaseAutoResolveHours') IS NOT NULL
            BEGIN
                DECLARE @crewBaseHoursDf sysname;
                SELECT @crewBaseHoursDf = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[Crews]') AND c.name = N'BaseAutoResolveHours';
                IF @crewBaseHoursDf IS NOT NULL
                    EXEC(N'ALTER TABLE [Crews] DROP CONSTRAINT [' + @crewBaseHoursDf + N']');
                ALTER TABLE [Crews] DROP COLUMN [BaseAutoResolveHours];
            END

            IF COL_LENGTH('Crews', 'ChangeAutoResolveTimerOnFirstReject') IS NOT NULL
            BEGIN
                DECLARE @crewChangeRejectDf sysname;
                SELECT @crewChangeRejectDf = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[Crews]') AND c.name = N'ChangeAutoResolveTimerOnFirstReject';
                IF @crewChangeRejectDf IS NOT NULL
                    EXEC(N'ALTER TABLE [Crews] DROP CONSTRAINT [' + @crewChangeRejectDf + N']');
                ALTER TABLE [Crews] DROP COLUMN [ChangeAutoResolveTimerOnFirstReject];
            END

            IF COL_LENGTH('Crews', 'AutoResolveHoursAfterFirstReject') IS NOT NULL
            BEGIN
                DECLARE @crewAfterRejectDf sysname;
                SELECT @crewAfterRejectDf = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[Crews]') AND c.name = N'AutoResolveHoursAfterFirstReject';
                IF @crewAfterRejectDf IS NOT NULL
                    EXEC(N'ALTER TABLE [Crews] DROP CONSTRAINT [' + @crewAfterRejectDf + N']');
                ALTER TABLE [Crews] DROP COLUMN [AutoResolveHoursAfterFirstReject];
            END

            IF COL_LENGTH('Fleets', 'AutoResolveOverTime') IS NOT NULL
            BEGIN
                DECLARE @fleetAutoResolveDf sysname;
                SELECT @fleetAutoResolveDf = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[Fleets]') AND c.name = N'AutoResolveOverTime';
                IF @fleetAutoResolveDf IS NOT NULL
                    EXEC(N'ALTER TABLE [Fleets] DROP CONSTRAINT [' + @fleetAutoResolveDf + N']');
                ALTER TABLE [Fleets] DROP COLUMN [AutoResolveOverTime];
            END

            IF COL_LENGTH('Fleets', 'BaseAutoResolveHours') IS NOT NULL
            BEGIN
                DECLARE @fleetBaseHoursDf sysname;
                SELECT @fleetBaseHoursDf = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[Fleets]') AND c.name = N'BaseAutoResolveHours';
                IF @fleetBaseHoursDf IS NOT NULL
                    EXEC(N'ALTER TABLE [Fleets] DROP CONSTRAINT [' + @fleetBaseHoursDf + N']');
                ALTER TABLE [Fleets] DROP COLUMN [BaseAutoResolveHours];
            END

            IF COL_LENGTH('Fleets', 'ChangeAutoResolveTimerOnFirstReject') IS NOT NULL
            BEGIN
                DECLARE @fleetChangeRejectDf sysname;
                SELECT @fleetChangeRejectDf = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[Fleets]') AND c.name = N'ChangeAutoResolveTimerOnFirstReject';
                IF @fleetChangeRejectDf IS NOT NULL
                    EXEC(N'ALTER TABLE [Fleets] DROP CONSTRAINT [' + @fleetChangeRejectDf + N']');
                ALTER TABLE [Fleets] DROP COLUMN [ChangeAutoResolveTimerOnFirstReject];
            END

            IF COL_LENGTH('Fleets', 'AutoResolveHoursAfterFirstReject') IS NOT NULL
            BEGIN
                DECLARE @fleetAfterRejectDf sysname;
                SELECT @fleetAfterRejectDf = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[Fleets]') AND c.name = N'AutoResolveHoursAfterFirstReject';
                IF @fleetAfterRejectDf IS NOT NULL
                    EXEC(N'ALTER TABLE [Fleets] DROP CONSTRAINT [' + @fleetAfterRejectDf + N']');
                ALTER TABLE [Fleets] DROP COLUMN [AutoResolveHoursAfterFirstReject];
            END
            """);
    }
}
