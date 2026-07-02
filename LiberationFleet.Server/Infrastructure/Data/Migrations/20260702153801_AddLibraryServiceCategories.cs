using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryServiceCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "LibraryCategories",
                columns: new[] { "Id", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 32, "Plumbing", 32 },
                    { 33, "Electrical Work", 33 },
                    { 34, "HVAC & AC", 34 },
                    { 35, "House Cleaning", 35 },
                    { 36, "Yard Work & Landscaping", 36 },
                    { 37, "Child Care", 37 },
                    { 38, "Car Maintenance & Repair", 38 },
                    { 39, "Home Renovations", 39 },
                    { 40, "Planning & Design", 40 },
                    { 41, "Physical Training & Coaching", 41 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 41);
        }
    }
}
