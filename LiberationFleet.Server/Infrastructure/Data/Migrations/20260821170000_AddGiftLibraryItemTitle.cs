using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260821170000_AddGiftLibraryItemTitle")]
    public partial class AddGiftLibraryItemTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: staging may have partially applied sibling migrations with the same timestamp.
            migrationBuilder.Sql("""
                IF COL_LENGTH('Gifts', 'LibraryItemTitle') IS NULL
                BEGIN
                    ALTER TABLE [Gifts] ADD [LibraryItemTitle] nvarchar(200) NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Gifts', 'LibraryItemTitle') IS NOT NULL
                BEGIN
                    ALTER TABLE [Gifts] DROP COLUMN [LibraryItemTitle];
                END
                """);
        }
    }
}
