using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillUserDisplayNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Real accounts: DisplayName defaults to Username so UI can always show DisplayName.
            migrationBuilder.Sql("""
                UPDATE Users
                SET DisplayName = Username
                WHERE IsUnclaimedPlaceholder = 0
                  AND IsCrewGiftRecipient = 0
                  AND (DisplayName IS NULL OR LTRIM(RTRIM(DisplayName)) = N'');
                """);

            // Crew gift sink account.
            migrationBuilder.Sql("""
                UPDATE Users
                SET DisplayName = N'the crew'
                WHERE IsCrewGiftRecipient = 1
                  AND (DisplayName IS NULL OR LTRIM(RTRIM(DisplayName)) = N'');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non-destructive: leave DisplayName values in place.
        }
    }
}
