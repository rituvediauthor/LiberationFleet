using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Infrastructure.Data;

/// <summary>
/// Ensures ApplicantDecision exists when the join-switch migration was skipped or undiscovered.
/// </summary>
public static class CrewJoinApplicantDecisionSchemaRepair
{
    private const string Sql = """
        IF COL_LENGTH('ProposalCrewJoinRequests', 'ApplicantDecision') IS NULL
        BEGIN
            ALTER TABLE [ProposalCrewJoinRequests] ADD [ApplicantDecision] int NOT NULL
                CONSTRAINT [DF_ProposalCrewJoinRequests_ApplicantDecision_Repair] DEFAULT 0;
        END
        """;

    public static async Task EnsureAsync(ApplicationDbContext dbContext, ILogger? logger = null)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(Sql);
            logger?.LogInformation("Crew join ApplicantDecision schema repair verified.");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Crew join ApplicantDecision schema repair failed.");
        }
    }
}
