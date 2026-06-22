using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEndToEndEncryption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrewKeyDistributions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrewId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    KeyVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    WrappedCrewKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WrapNonce = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WrappedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrewKeyDistributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrewKeyDistributions_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrewKeyDistributions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrewKeyDistributions_Users_WrappedByUserId",
                        column: x => x.WrappedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EncryptedContentEnvelopes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContentType = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CrewId = table.Column<int>(type: "int", nullable: true),
                    AuthorUserId = table.Column<int>(type: "int", nullable: false),
                    KeyVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Nonce = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ciphertext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncryptedContentEnvelopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncryptedContentEnvelopes_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EncryptedContentEnvelopes_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserKeyBundles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    IdentityPublicKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserKeyBundles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserKeyBundles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPrivateKeyBackups",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Iv = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ciphertext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPrivateKeyBackups", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserPrivateKeyBackups_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrewKeyDistributions_CrewId_UserId_KeyVersion",
                table: "CrewKeyDistributions",
                columns: new[] { "CrewId", "UserId", "KeyVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrewKeyDistributions_UserId",
                table: "CrewKeyDistributions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrewKeyDistributions_WrappedByUserId",
                table: "CrewKeyDistributions",
                column: "WrappedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EncryptedContentEnvelopes_AuthorUserId",
                table: "EncryptedContentEnvelopes",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EncryptedContentEnvelopes_ContentType_ResourceId",
                table: "EncryptedContentEnvelopes",
                columns: new[] { "ContentType", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EncryptedContentEnvelopes_CrewId",
                table: "EncryptedContentEnvelopes",
                column: "CrewId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrewKeyDistributions");

            migrationBuilder.DropTable(
                name: "EncryptedContentEnvelopes");

            migrationBuilder.DropTable(
                name: "UserKeyBundles");

            migrationBuilder.DropTable(
                name: "UserPrivateKeyBackups");
        }
    }
}
