using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncSecurityStampFriendshipMediaRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EncryptedContentEnvelopes_CrewOrFleet",
                table: "EncryptedContentEnvelopes");

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE Users
                SET SecurityStamp = LOWER(REPLACE(CONVERT(nvarchar(36), NEWID()), '-', ''))
                WHERE SecurityStamp = '' OR SecurityStamp IS NULL;
                """);

            migrationBuilder.AddColumn<int>(
                name: "UserHighId",
                table: "Friendships",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserLowId",
                table: "Friendships",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE Friendships
                SET UserLowId = CASE WHEN RequesterUserId < AddresseeUserId THEN RequesterUserId ELSE AddresseeUserId END,
                    UserHighId = CASE WHEN RequesterUserId < AddresseeUserId THEN AddresseeUserId ELSE RequesterUserId END;
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY UserLowId, UserHighId ORDER BY CreatedAt ASC, Id ASC) AS rn
                    FROM Friendships
                )
                DELETE FROM Friendships
                WHERE Id IN (SELECT Id FROM ranked WHERE rn > 1);
                """);

            migrationBuilder.AddColumn<int>(
                name: "RecipientUserId",
                table: "EncryptedContentEnvelopes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_UserLowId_UserHighId",
                table: "Friendships",
                columns: new[] { "UserLowId", "UserHighId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EncryptedContentEnvelopes_RecipientUserId",
                table: "EncryptedContentEnvelopes",
                column: "RecipientUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EncryptedContentEnvelopes_CrewOrFleet",
                table: "EncryptedContentEnvelopes",
                sql: "([CrewId] IS NOT NULL AND [FleetId] IS NULL) OR ([CrewId] IS NULL AND [FleetId] IS NOT NULL) OR ([CrewId] IS NULL AND [FleetId] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Friendships_UserLowId_UserHighId",
                table: "Friendships");

            migrationBuilder.DropIndex(
                name: "IX_EncryptedContentEnvelopes_RecipientUserId",
                table: "EncryptedContentEnvelopes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EncryptedContentEnvelopes_CrewOrFleet",
                table: "EncryptedContentEnvelopes");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserHighId",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "UserLowId",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "EncryptedContentEnvelopes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EncryptedContentEnvelopes_CrewOrFleet",
                table: "EncryptedContentEnvelopes",
                sql: "[CrewId] IS NOT NULL OR [FleetId] IS NOT NULL");
        }
    }
}
