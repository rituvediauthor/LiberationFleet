using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

/// <summary>
/// Pre-season aid-stat draft JSON and auto-join-at-season-start flag on crew memberships.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260929180000_AddCrewmateAidStatSeasonAccounting")]
public partial class AddCrewmateAidStatSeasonAccounting : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('CrewMemberships', 'AutoJoinSeasonOnStart') IS NULL
            BEGIN
                ALTER TABLE [CrewMemberships] ADD [AutoJoinSeasonOnStart] bit NOT NULL
                    CONSTRAINT [DF_CrewMemberships_AutoJoinSeasonOnStart] DEFAULT 0;
            END

            IF COL_LENGTH('CrewMemberships', 'AidStatDraftJson') IS NULL
            BEGIN
                ALTER TABLE [CrewMemberships] ADD [AidStatDraftJson] nvarchar(max) NULL;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('CrewMemberships', 'AutoJoinSeasonOnStart') IS NOT NULL
            BEGIN
                DECLARE @constraint sysname;
                SELECT @constraint = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[CrewMemberships]')
                  AND c.name = N'AutoJoinSeasonOnStart';
                IF @constraint IS NOT NULL
                    EXEC(N'ALTER TABLE [CrewMemberships] DROP CONSTRAINT [' + @constraint + N']');
                ALTER TABLE [CrewMemberships] DROP COLUMN [AutoJoinSeasonOnStart];
            END

            IF COL_LENGTH('CrewMemberships', 'AidStatDraftJson') IS NOT NULL
            BEGIN
                ALTER TABLE [CrewMemberships] DROP COLUMN [AidStatDraftJson];
            END
            """);
    }
}
