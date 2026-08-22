using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Remaps historical proposal comment notifications from NewReply/NewFleetReply
    /// to NewProposalReply/NewFleetProposalReply based on ActionUrl.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260822180000_RemapProposalReplyNotificationKinds")]
    public partial class RemapProposalReplyNotificationKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NewReply (8) -> NewProposalReply (53) for proposal URLs
            migrationBuilder.Sql("""
                UPDATE [Notifications]
                SET [Kind] = 53
                WHERE [Kind] = 8
                  AND (
                    [ActionUrl] LIKE N'/app/crew/proposals/%'
                    OR [ActionUrl] LIKE N'/app/fleet/proposals/%'
                  );
                """);

            // NewFleetReply (45) -> NewFleetProposalReply (54) for fleet proposal URLs
            migrationBuilder.Sql("""
                UPDATE [Notifications]
                SET [Kind] = 54
                WHERE [Kind] = 45
                  AND [ActionUrl] LIKE N'/app/fleet/proposals/%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [Notifications]
                SET [Kind] = 8
                WHERE [Kind] = 53;
                """);

            migrationBuilder.Sql("""
                UPDATE [Notifications]
                SET [Kind] = 45
                WHERE [Kind] = 54;
                """);
        }
    }
}
