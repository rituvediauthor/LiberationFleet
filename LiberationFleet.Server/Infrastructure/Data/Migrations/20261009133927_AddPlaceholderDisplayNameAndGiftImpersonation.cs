using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceholderDisplayNameAndGiftImpersonation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImpersonatedByUserId",
                table: "Gifts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_ImpersonatedByUserId",
                table: "Gifts",
                column: "ImpersonatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gifts_Users_ImpersonatedByUserId",
                table: "Gifts",
                column: "ImpersonatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gifts_Users_ImpersonatedByUserId",
                table: "Gifts");

            migrationBuilder.DropIndex(
                name: "IX_Gifts_ImpersonatedByUserId",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ImpersonatedByUserId",
                table: "Gifts");
        }
    }
}
