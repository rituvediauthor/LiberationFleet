using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using LiberationFleet.Server.Infrastructure.Data;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260727180000_AddForumLikesAndDonationUrgency")]
    public partial class AddForumLikesAndDonationUrgency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DonationCampaignPhaseShownCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DonationCampaignPhaseTarget",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DonationCampaignUrgencyPhase",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ForumLikes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ForumPostId = table.Column<int>(type: "int", nullable: true),
                    ForumCommentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuthorNotified = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumLikes", x => x.Id);
                    table.CheckConstraint(
                        "CK_ForumLikes_PostOrComment",
                        "([ForumPostId] IS NOT NULL AND [ForumCommentId] IS NULL) OR ([ForumPostId] IS NULL AND [ForumCommentId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ForumLikes_ForumComments_ForumCommentId",
                        column: x => x.ForumCommentId,
                        principalTable: "ForumComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ForumLikes_ForumPosts_ForumPostId",
                        column: x => x.ForumPostId,
                        principalTable: "ForumPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ForumLikes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForumLikes_ForumCommentId",
                table: "ForumLikes",
                column: "ForumCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumLikes_ForumPostId",
                table: "ForumLikes",
                column: "ForumPostId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumLikes_UserId_ForumCommentId",
                table: "ForumLikes",
                columns: new[] { "UserId", "ForumCommentId" },
                unique: true,
                filter: "[ForumCommentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ForumLikes_UserId_ForumPostId",
                table: "ForumLikes",
                columns: new[] { "UserId", "ForumPostId" },
                unique: true,
                filter: "[ForumPostId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForumLikes");

            migrationBuilder.DropColumn(
                name: "DonationCampaignPhaseShownCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DonationCampaignPhaseTarget",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DonationCampaignUrgencyPhase",
                table: "Users");
        }
    }
}
