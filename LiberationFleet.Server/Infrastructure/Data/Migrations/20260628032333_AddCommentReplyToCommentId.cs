using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentReplyToCommentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReplyToCommentId",
                table: "ProposalComments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplyToCommentId",
                table: "ProjectComments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplyToCommentId",
                table: "ForumComments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplyToCommentId",
                table: "ProposalComments");

            migrationBuilder.DropColumn(
                name: "ReplyToCommentId",
                table: "ProjectComments");

            migrationBuilder.DropColumn(
                name: "ReplyToCommentId",
                table: "ForumComments");
        }
    }
}
