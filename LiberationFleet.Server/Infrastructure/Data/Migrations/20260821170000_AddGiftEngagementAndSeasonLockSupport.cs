using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260821170000_AddGiftEngagementAndSeasonLockSupport")]
    public partial class AddGiftEngagementAndSeasonLockSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GiftComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GiftId = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<int>(type: "int", nullable: false),
                    ParentCommentId = table.Column<int>(type: "int", nullable: true),
                    ReplyToCommentId = table.Column<int>(type: "int", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiftComments_Gifts_GiftId",
                        column: x => x.GiftId,
                        principalTable: "Gifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GiftComments_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GiftComments_GiftComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "GiftComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GiftComments_GiftComments_ReplyToCommentId",
                        column: x => x.ReplyToCommentId,
                        principalTable: "GiftComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GiftLikes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GiftId = table.Column<int>(type: "int", nullable: true),
                    GiftCommentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuthorNotified = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftLikes", x => x.Id);
                    table.CheckConstraint(
                        "CK_GiftLikes_GiftOrComment",
                        "([GiftId] IS NOT NULL AND [GiftCommentId] IS NULL) OR ([GiftId] IS NULL AND [GiftCommentId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_GiftLikes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GiftLikes_Gifts_GiftId",
                        column: x => x.GiftId,
                        principalTable: "Gifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GiftLikes_GiftComments_GiftCommentId",
                        column: x => x.GiftCommentId,
                        principalTable: "GiftComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GiftComments_GiftId",
                table: "GiftComments",
                column: "GiftId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftComments_AuthorUserId",
                table: "GiftComments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftComments_ParentCommentId",
                table: "GiftComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftComments_ReplyToCommentId",
                table: "GiftComments",
                column: "ReplyToCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftLikes_UserId_GiftId",
                table: "GiftLikes",
                columns: new[] { "UserId", "GiftId" },
                unique: true,
                filter: "[GiftId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GiftLikes_UserId_GiftCommentId",
                table: "GiftLikes",
                columns: new[] { "UserId", "GiftCommentId" },
                unique: true,
                filter: "[GiftCommentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GiftLikes_GiftId",
                table: "GiftLikes",
                column: "GiftId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftLikes_GiftCommentId",
                table: "GiftLikes",
                column: "GiftCommentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GiftLikes");
            migrationBuilder.DropTable(name: "GiftComments");
        }
    }
}
