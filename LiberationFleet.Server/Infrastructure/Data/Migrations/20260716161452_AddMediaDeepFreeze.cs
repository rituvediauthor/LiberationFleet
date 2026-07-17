using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaDeepFreeze : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CiphertextCharLength",
                table: "EncryptedContentEnvelopes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ColdBlobPath",
                table: "EncryptedContentEnvelopes",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FrozenAt",
                table: "EncryptedContentEnvelopes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageTier",
                table: "EncryptedContentEnvelopes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EncryptedContentEnvelopes_StorageTier_CreatedAt",
                table: "EncryptedContentEnvelopes",
                columns: new[] { "StorageTier", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EncryptedContentEnvelopes_StorageTier_CreatedAt",
                table: "EncryptedContentEnvelopes");

            migrationBuilder.DropColumn(
                name: "CiphertextCharLength",
                table: "EncryptedContentEnvelopes");

            migrationBuilder.DropColumn(
                name: "ColdBlobPath",
                table: "EncryptedContentEnvelopes");

            migrationBuilder.DropColumn(
                name: "FrozenAt",
                table: "EncryptedContentEnvelopes");

            migrationBuilder.DropColumn(
                name: "StorageTier",
                table: "EncryptedContentEnvelopes");
        }
    }
}
