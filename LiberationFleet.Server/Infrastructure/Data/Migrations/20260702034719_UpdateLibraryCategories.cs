using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LiberationFleet.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLibraryCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Produce & Fresh Foods");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "Meat & Seafood");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "Dairy & Eggs");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "Bakery & Bread");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 5,
                column: "Name",
                value: "Frozen Foods");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 6,
                column: "Name",
                value: "Pantry & Dry Goods");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 7,
                column: "Name",
                value: "Beverages");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 8,
                column: "Name",
                value: "Snacks & Candy");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 9,
                column: "Name",
                value: "Deli & Prepared Foods");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 10,
                column: "Name",
                value: "Health & Personal Care");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 11,
                column: "Name",
                value: "Baby & Childcare");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 12,
                column: "Name",
                value: "Pet Supplies");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "Name", "SortOrder" },
                values: new object[] { "Household & Cleaning", 13 });

            migrationBuilder.InsertData(
                table: "LibraryCategories",
                columns: new[] { "Id", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 14, "Kitchen & Dining", 14 },
                    { 15, "Home & Furniture", 15 },
                    { 16, "Bedding & Bath", 16 },
                    { 17, "Apparel & Accessories", 17 },
                    { 18, "Shoes", 18 },
                    { 19, "Tools & Hardware", 19 },
                    { 20, "Garden & Outdoor", 20 },
                    { 21, "Electronics", 21 },
                    { 22, "Appliances", 22 },
                    { 23, "Sports & Fitness", 23 },
                    { 24, "Books, Movies & Music", 24 },
                    { 25, "Toys & Games", 25 },
                    { 26, "Automotive", 26 },
                    { 27, "Office & School Supplies", 27 },
                    { 28, "Pharmacy & Wellness", 28 },
                    { 29, "Arts & Crafts", 29 },
                    { 30, "Party & Seasonal", 30 },
                    { 31, "Services & Skills", 31 },
                    { 99, "Other", 99 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 99);

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Clothes");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "Food");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "Vehicles");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "Tools");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 5,
                column: "Name",
                value: "Shelter");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 6,
                column: "Name",
                value: "Home");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 7,
                column: "Name",
                value: "Art");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 8,
                column: "Name",
                value: "Entertainment");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 9,
                column: "Name",
                value: "Editing");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 10,
                column: "Name",
                value: "Software");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 11,
                column: "Name",
                value: "Plumbing");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 12,
                column: "Name",
                value: "Lawncare");

            migrationBuilder.UpdateData(
                table: "LibraryCategories",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "Name", "SortOrder" },
                values: new object[] { "Other", 99 });
        }
    }
}
