using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

/// <summary>
/// Applicant Switch/Stay decision when a join request is approved while they are already in a crew.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260916140000_AddCrewJoinApplicantDecision")]
public partial class AddCrewJoinApplicantDecision : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('ProposalCrewJoinRequests', 'ApplicantDecision') IS NULL
            BEGIN
                ALTER TABLE [ProposalCrewJoinRequests] ADD [ApplicantDecision] int NOT NULL
                    CONSTRAINT [DF_ProposalCrewJoinRequests_ApplicantDecision] DEFAULT 0;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('ProposalCrewJoinRequests', 'ApplicantDecision') IS NOT NULL
            BEGIN
                DECLARE @constraint sysname;
                SELECT @constraint = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[ProposalCrewJoinRequests]')
                  AND c.name = N'ApplicantDecision';
                IF @constraint IS NOT NULL
                    EXEC(N'ALTER TABLE [ProposalCrewJoinRequests] DROP CONSTRAINT [' + @constraint + N']');
                ALTER TABLE [ProposalCrewJoinRequests] DROP COLUMN [ApplicantDecision];
            END
            """);
    }
}
