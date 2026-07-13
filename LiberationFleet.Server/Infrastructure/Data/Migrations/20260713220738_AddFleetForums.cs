using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetForums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CrewId",
                table: "ForumPosts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "ForumPosts",
                type: "nvarchar(max)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FleetId",
                table: "ForumPosts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ForumPosts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "ForumComments",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumPosts_FleetId",
                table: "ForumPosts",
                column: "FleetId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ForumPosts_CrewOrFleet",
                table: "ForumPosts",
                sql: "[CrewId] IS NOT NULL OR [FleetId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ForumPosts_Fleets_FleetId",
                table: "ForumPosts",
                column: "FleetId",
                principalTable: "Fleets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ForumPosts_Fleets_FleetId",
                table: "ForumPosts");

            migrationBuilder.DropIndex(
                name: "IX_ForumPosts_FleetId",
                table: "ForumPosts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ForumPosts_CrewOrFleet",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "FleetId",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "ForumComments");

            migrationBuilder.AlterColumn<int>(
                name: "CrewId",
                table: "ForumPosts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
