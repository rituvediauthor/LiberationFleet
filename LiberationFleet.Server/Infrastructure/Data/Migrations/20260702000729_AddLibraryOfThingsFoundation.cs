using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryOfThingsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LibraryCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LibraryOfferings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrewId = table.Column<int>(type: "int", nullable: false),
                    CreatorUserId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    FulfillmentMode = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleNormalized = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DescriptionPreview = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ValuePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitLabel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RemainingStock = table.Column<int>(type: "int", nullable: true),
                    QuantityNotApplicable = table.Column<bool>(type: "bit", nullable: false),
                    ThumbnailResourceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HasEncryptedContent = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryOfferings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LibraryOfferings_Crews_CrewId",
                        column: x => x.CrewId,
                        principalTable: "Crews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryOfferings_Users_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LibraryOfferingCategories",
                columns: table => new
                {
                    OfferingId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryOfferingCategories", x => new { x.OfferingId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_LibraryOfferingCategories_LibraryCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "LibraryCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LibraryOfferingCategories_LibraryOfferings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "LibraryOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LibraryUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfferingId = table.Column<int>(type: "int", nullable: false),
                    CurrentPossessorUserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LibraryUnits_LibraryOfferings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "LibraryOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LibraryUnits_Users_CurrentPossessorUserId",
                        column: x => x.CurrentPossessorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "LibraryCategories",
                columns: new[] { "Id", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, "Clothes", 1 },
                    { 2, "Food", 2 },
                    { 3, "Vehicles", 3 },
                    { 4, "Tools", 4 },
                    { 5, "Shelter", 5 },
                    { 6, "Home", 6 },
                    { 7, "Art", 7 },
                    { 8, "Entertainment", 8 },
                    { 9, "Editing", 9 },
                    { 10, "Software", 10 },
                    { 11, "Plumbing", 11 },
                    { 12, "Lawncare", 12 },
                    { 13, "Other", 99 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryOfferingCategories_CategoryId",
                table: "LibraryOfferingCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryOfferings_CreatorUserId",
                table: "LibraryOfferings",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryOfferings_CrewId",
                table: "LibraryOfferings",
                column: "CrewId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryUnits_CurrentPossessorUserId",
                table: "LibraryUnits",
                column: "CurrentPossessorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryUnits_OfferingId",
                table: "LibraryUnits",
                column: "OfferingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LibraryOfferingCategories");

            migrationBuilder.DropTable(
                name: "LibraryUnits");

            migrationBuilder.DropTable(
                name: "LibraryCategories");

            migrationBuilder.DropTable(
                name: "LibraryOfferings");
        }
    }
}
