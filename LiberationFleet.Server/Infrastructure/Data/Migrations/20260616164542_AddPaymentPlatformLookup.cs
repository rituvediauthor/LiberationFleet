using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentPlatformLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentPlatforms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPlatforms", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PaymentPlatforms",
                columns: new[] { "Id", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, "PayPal", 1 },
                    { 2, "Cash App", 2 },
                    { 3, "Venmo", 3 },
                    { 4, "Zelle", 4 },
                    { 5, "Other", 5 }
                });

            migrationBuilder.AddColumn<int>(
                name: "PaymentPlatformId",
                table: "UserPaymentPlatforms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentPlatformId",
                table: "Gifts",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE upp
                SET PaymentPlatformId = COALESCE(pp.Id, 5)
                FROM UserPaymentPlatforms upp
                LEFT JOIN PaymentPlatforms pp ON pp.Name = upp.Platform
                """);

            migrationBuilder.Sql("""
                UPDATE g
                SET PaymentPlatformId = COALESCE(pp.Id, 5)
                FROM Gifts g
                LEFT JOIN PaymentPlatforms pp ON pp.Name = g.Platform
                """);

            migrationBuilder.DropColumn(
                name: "Platform",
                table: "UserPaymentPlatforms");

            migrationBuilder.DropColumn(
                name: "Platform",
                table: "Gifts");

            migrationBuilder.AlterColumn<int>(
                name: "PaymentPlatformId",
                table: "UserPaymentPlatforms",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PaymentPlatformId",
                table: "Gifts",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPaymentPlatforms_PaymentPlatformId",
                table: "UserPaymentPlatforms",
                column: "PaymentPlatformId");

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_PaymentPlatformId",
                table: "Gifts",
                column: "PaymentPlatformId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gifts_PaymentPlatforms_PaymentPlatformId",
                table: "Gifts",
                column: "PaymentPlatformId",
                principalTable: "PaymentPlatforms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPaymentPlatforms_PaymentPlatforms_PaymentPlatformId",
                table: "UserPaymentPlatforms",
                column: "PaymentPlatformId",
                principalTable: "PaymentPlatforms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gifts_PaymentPlatforms_PaymentPlatformId",
                table: "Gifts");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPaymentPlatforms_PaymentPlatforms_PaymentPlatformId",
                table: "UserPaymentPlatforms");

            migrationBuilder.DropIndex(
                name: "IX_UserPaymentPlatforms_PaymentPlatformId",
                table: "UserPaymentPlatforms");

            migrationBuilder.DropIndex(
                name: "IX_Gifts_PaymentPlatformId",
                table: "Gifts");

            migrationBuilder.AddColumn<string>(
                name: "Platform",
                table: "UserPaymentPlatforms",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Platform",
                table: "Gifts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE upp
                SET Platform = pp.Name
                FROM UserPaymentPlatforms upp
                INNER JOIN PaymentPlatforms pp ON pp.Id = upp.PaymentPlatformId
                """);

            migrationBuilder.Sql("""
                UPDATE g
                SET Platform = pp.Name
                FROM Gifts g
                INNER JOIN PaymentPlatforms pp ON pp.Id = g.PaymentPlatformId
                """);

            migrationBuilder.DropColumn(
                name: "PaymentPlatformId",
                table: "UserPaymentPlatforms");

            migrationBuilder.DropColumn(
                name: "PaymentPlatformId",
                table: "Gifts");

            migrationBuilder.DropTable(
                name: "PaymentPlatforms");
        }
    }
}
