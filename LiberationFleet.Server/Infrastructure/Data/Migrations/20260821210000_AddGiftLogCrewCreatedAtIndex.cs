using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Speeds gift-log pagination (ORDER BY CreatedAt DESC, Id DESC per crew).
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260821210000_AddGiftLogCrewCreatedAtIndex")]
    public partial class AddGiftLogCrewCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Gifts_CrewId_CreatedAt_Id'
                      AND object_id = OBJECT_ID(N'[Gifts]')
                )
                BEGIN
                    CREATE INDEX [IX_Gifts_CrewId_CreatedAt_Id]
                    ON [Gifts] ([CrewId], [CreatedAt], [Id]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Gifts_CrewId_CreatedAt_Id'
                      AND object_id = OBJECT_ID(N'[Gifts]')
                )
                BEGIN
                    DROP INDEX [IX_Gifts_CrewId_CreatedAt_Id] ON [Gifts];
                END
                """);
        }
    }
}
