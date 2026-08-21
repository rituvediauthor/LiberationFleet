using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Infrastructure.Data;

/// <summary>
/// Repairs gift-log schema when same-timestamp migrations left staging
/// with __EFMigrationsHistory rows but incomplete columns/tables.
/// </summary>
public static class GiftLogSchemaRepair
{
    public const string EnsureSql = """
        IF COL_LENGTH('Gifts', 'LibraryItemTitle') IS NULL
        BEGIN
            ALTER TABLE [Gifts] ADD [LibraryItemTitle] nvarchar(200) NULL;
        END

        IF OBJECT_ID(N'[GiftComments]', N'U') IS NULL
        BEGIN
            CREATE TABLE [GiftComments] (
                [Id] int NOT NULL IDENTITY,
                [GiftId] int NOT NULL,
                [AuthorUserId] int NOT NULL,
                [ParentCommentId] int NULL,
                [ReplyToCommentId] int NULL,
                [Body] nvarchar(4000) NULL,
                [CreatedAt] datetime2 NOT NULL,
                [IsDeleted] bit NOT NULL,
                CONSTRAINT [PK_GiftComments] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_GiftComments_Gifts_GiftId] FOREIGN KEY ([GiftId]) REFERENCES [Gifts] ([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_GiftComments_Users_AuthorUserId] FOREIGN KEY ([AuthorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_GiftComments_GiftComments_ParentCommentId] FOREIGN KEY ([ParentCommentId]) REFERENCES [GiftComments] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_GiftComments_GiftComments_ReplyToCommentId] FOREIGN KEY ([ReplyToCommentId]) REFERENCES [GiftComments] ([Id]) ON DELETE NO ACTION
            );
            CREATE INDEX [IX_GiftComments_GiftId] ON [GiftComments] ([GiftId]);
            CREATE INDEX [IX_GiftComments_AuthorUserId] ON [GiftComments] ([AuthorUserId]);
            CREATE INDEX [IX_GiftComments_ParentCommentId] ON [GiftComments] ([ParentCommentId]);
            CREATE INDEX [IX_GiftComments_ReplyToCommentId] ON [GiftComments] ([ReplyToCommentId]);
        END

        IF OBJECT_ID(N'[GiftLikes]', N'U') IS NULL
        BEGIN
            CREATE TABLE [GiftLikes] (
                [Id] int NOT NULL IDENTITY,
                [UserId] int NOT NULL,
                [GiftId] int NULL,
                [GiftCommentId] int NULL,
                [CreatedAt] datetime2 NOT NULL,
                [RemovedAt] datetime2 NULL,
                [AuthorNotified] bit NOT NULL CONSTRAINT [DF_GiftLikes_AuthorNotified_Repair] DEFAULT CAST(0 AS bit),
                CONSTRAINT [PK_GiftLikes] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_GiftLikes_GiftOrComment] CHECK (([GiftId] IS NOT NULL AND [GiftCommentId] IS NULL) OR ([GiftId] IS NULL AND [GiftCommentId] IS NOT NULL)),
                CONSTRAINT [FK_GiftLikes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_GiftLikes_Gifts_GiftId] FOREIGN KEY ([GiftId]) REFERENCES [Gifts] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [FK_GiftLikes_GiftComments_GiftCommentId] FOREIGN KEY ([GiftCommentId]) REFERENCES [GiftComments] ([Id]) ON DELETE NO ACTION
            );
            CREATE UNIQUE INDEX [IX_GiftLikes_UserId_GiftId] ON [GiftLikes] ([UserId], [GiftId]) WHERE [GiftId] IS NOT NULL;
            CREATE UNIQUE INDEX [IX_GiftLikes_UserId_GiftCommentId] ON [GiftLikes] ([UserId], [GiftCommentId]) WHERE [GiftCommentId] IS NOT NULL;
            CREATE INDEX [IX_GiftLikes_GiftId] ON [GiftLikes] ([GiftId]);
            CREATE INDEX [IX_GiftLikes_GiftCommentId] ON [GiftLikes] ([GiftCommentId]);
        END
        """;

    public static async Task EnsureAsync(
        ApplicationDbContext dbContext,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(EnsureSql, cancellationToken);
            logger?.LogInformation("Gift log schema repair verified (LibraryItemTitle, GiftComments, GiftLikes).");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Gift log schema repair failed.");
            throw;
        }
    }
}
