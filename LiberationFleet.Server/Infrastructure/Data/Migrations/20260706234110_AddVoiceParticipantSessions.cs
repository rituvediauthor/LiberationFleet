using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceParticipantSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VoiceParticipantSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CrewId = table.Column<int>(type: "int", nullable: false),
                    ChatRoomId = table.Column<int>(type: "int", nullable: false),
                    ConnectionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsMuted = table.Column<bool>(type: "bit", nullable: false),
                    IsDeafened = table.Column<bool>(type: "bit", nullable: false),
                    IsSpeaking = table.Column<bool>(type: "bit", nullable: false),
                    IsServerMuted = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoiceParticipantSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoiceParticipantSessions_ChatRooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoiceParticipantSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoiceParticipantSessions_ChatRoomId",
                table: "VoiceParticipantSessions",
                column: "ChatRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_VoiceParticipantSessions_ConnectionId",
                table: "VoiceParticipantSessions",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_VoiceParticipantSessions_UserId_CrewId",
                table: "VoiceParticipantSessions",
                columns: new[] { "UserId", "CrewId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoiceParticipantSessions");
        }
    }
}
