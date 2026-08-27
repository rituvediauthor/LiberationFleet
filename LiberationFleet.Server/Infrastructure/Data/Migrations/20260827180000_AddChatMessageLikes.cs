using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260827180000_AddChatMessageLikes")]
public partial class AddChatMessageLikes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[ChatMessageLikes]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ChatMessageLikes] (
                    [Id] int NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [ChatRoomMessageId] int NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [RemovedAt] datetime2 NULL,
                    [AuthorNotified] bit NOT NULL CONSTRAINT [DF_ChatMessageLikes_AuthorNotified] DEFAULT CAST(0 AS bit),
                    CONSTRAINT [PK_ChatMessageLikes] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ChatMessageLikes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_ChatMessageLikes_ChatRoomMessages_ChatRoomMessageId]
                        FOREIGN KEY ([ChatRoomMessageId]) REFERENCES [ChatRoomMessages] ([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_ChatMessageLikes_UserId_ChatRoomMessageId]
                    ON [ChatMessageLikes] ([UserId], [ChatRoomMessageId]);
                CREATE INDEX [IX_ChatMessageLikes_ChatRoomMessageId]
                    ON [ChatMessageLikes] ([ChatRoomMessageId]);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[ChatMessageLikes]', N'U') IS NOT NULL
                DROP TABLE [ChatMessageLikes];
            """);
    }
}
