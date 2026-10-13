using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftVerifiedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VerifiedByUserId",
                table: "Gifts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_VerifiedByUserId",
                table: "Gifts",
                column: "VerifiedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gifts_Users_VerifiedByUserId",
                table: "Gifts",
                column: "VerifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gifts_Users_VerifiedByUserId",
                table: "Gifts");

            migrationBuilder.DropIndex(
                name: "IX_Gifts_VerifiedByUserId",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "Gifts");
        }
    }
}
