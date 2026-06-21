using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrewPaymentPlatforms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrewPaymentPlatforms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrewId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrewPaymentPlatforms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrewPaymentPlatforms_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrewPaymentPlatforms_CrewId_Name",
                table: "CrewPaymentPlatforms",
                columns: new[] { "CrewId", "Name" },
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "CrewPaymentPlatformId",
                table: "UserPaymentPlatforms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPreferred",
                table: "UserPaymentPlatforms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CrewPaymentPlatformId",
                table: "Gifts",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO CrewPaymentPlatforms (CrewId, Name)
                SELECT DISTINCT cm.CrewId, pp.Name
                FROM UserPaymentPlatforms upp
                INNER JOIN Users u ON u.Id = upp.UserId
                INNER JOIN CrewMemberships cm ON cm.UserId = u.Id AND cm.IsBanned = 0
                INNER JOIN PaymentPlatforms pp ON pp.Id = upp.PaymentPlatformId
                WHERE NOT EXISTS (
                    SELECT 1 FROM CrewPaymentPlatforms cpp
                    WHERE cpp.CrewId = cm.CrewId AND cpp.Name = pp.Name);

                INSERT INTO CrewPaymentPlatforms (CrewId, Name)
                SELECT DISTINCT g.CrewId, pp.Name
                FROM Gifts g
                INNER JOIN PaymentPlatforms pp ON pp.Id = g.PaymentPlatformId
                WHERE NOT EXISTS (
                    SELECT 1 FROM CrewPaymentPlatforms cpp
                    WHERE cpp.CrewId = g.CrewId AND cpp.Name = pp.Name);

                UPDATE upp
                SET upp.CrewPaymentPlatformId = cpp.Id
                FROM UserPaymentPlatforms upp
                INNER JOIN Users u ON u.Id = upp.UserId
                INNER JOIN CrewMemberships cm ON cm.UserId = u.Id AND cm.IsBanned = 0
                INNER JOIN PaymentPlatforms pp ON pp.Id = upp.PaymentPlatformId
                INNER JOIN CrewPaymentPlatforms cpp ON cpp.CrewId = cm.CrewId AND cpp.Name = pp.Name;

                UPDATE g
                SET g.CrewPaymentPlatformId = cpp.Id
                FROM Gifts g
                INNER JOIN PaymentPlatforms pp ON pp.Id = g.PaymentPlatformId
                INNER JOIN CrewPaymentPlatforms cpp ON cpp.CrewId = g.CrewId AND cpp.Name = pp.Name;

                UPDATE UserPaymentPlatforms
                SET IsPreferred = 1
                WHERE Id IN (
                    SELECT MIN(Id) FROM UserPaymentPlatforms GROUP BY UserId);
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Gifts_PaymentPlatforms_PaymentPlatformId",
                table: "Gifts");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPaymentPlatforms_PaymentPlatforms_PaymentPlatformId",
                table: "UserPaymentPlatforms");

            migrationBuilder.DropIndex(
                name: "IX_Gifts_PaymentPlatformId",
                table: "Gifts");

            migrationBuilder.DropIndex(
                name: "IX_UserPaymentPlatforms_PaymentPlatformId",
                table: "UserPaymentPlatforms");

            migrationBuilder.DropColumn(
                name: "PaymentPlatformId",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "PaymentPlatformId",
                table: "UserPaymentPlatforms");

            migrationBuilder.AlterColumn<int>(
                name: "CrewPaymentPlatformId",
                table: "UserPaymentPlatforms",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CrewPaymentPlatformId",
                table: "Gifts",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPaymentPlatforms_CrewPaymentPlatformId",
                table: "UserPaymentPlatforms",
                column: "CrewPaymentPlatformId");

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_CrewPaymentPlatformId",
                table: "Gifts",
                column: "CrewPaymentPlatformId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gifts_CrewPaymentPlatforms_CrewPaymentPlatformId",
                table: "Gifts",
                column: "CrewPaymentPlatformId",
                principalTable: "CrewPaymentPlatforms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPaymentPlatforms_CrewPaymentPlatforms_CrewPaymentPlatformId",
                table: "UserPaymentPlatforms",
                column: "CrewPaymentPlatformId",
                principalTable: "CrewPaymentPlatforms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gifts_CrewPaymentPlatforms_CrewPaymentPlatformId",
                table: "Gifts");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPaymentPlatforms_CrewPaymentPlatforms_CrewPaymentPlatformId",
                table: "UserPaymentPlatforms");

            migrationBuilder.DropTable(
                name: "CrewPaymentPlatforms");

            migrationBuilder.DropColumn(
                name: "IsPreferred",
                table: "UserPaymentPlatforms");

            migrationBuilder.DropColumn(
                name: "CrewPaymentPlatformId",
                table: "UserPaymentPlatforms");

            migrationBuilder.DropColumn(
                name: "CrewPaymentPlatformId",
                table: "Gifts");

            migrationBuilder.AddColumn<int>(
                name: "PaymentPlatformId",
                table: "UserPaymentPlatforms",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PaymentPlatformId",
                table: "Gifts",
                type: "int",
                nullable: false,
                defaultValue: 1);

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
    }
}
