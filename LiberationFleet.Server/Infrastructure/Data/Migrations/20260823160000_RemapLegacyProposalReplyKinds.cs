using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Catch legacy proposal comment notifications missed by the first remap migration.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260823160000_RemapLegacyProposalReplyKinds")]
    public partial class RemapLegacyProposalReplyKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [Notifications]
                SET [Kind] = 53
                WHERE [Kind] = 8
                  AND [ActionUrl] LIKE N'%/proposals%';
                """);

            migrationBuilder.Sql("""
                UPDATE [Notifications]
                SET [Kind] = 54
                WHERE [Kind] = 45
                  AND [ActionUrl] LIKE N'%/proposals%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only forward fix; no reliable down.
        }
    }
}
