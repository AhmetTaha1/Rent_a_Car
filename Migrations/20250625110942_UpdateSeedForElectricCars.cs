using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace rent_a_car.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSeedForElectricCars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { "1.2L", "Petrol", "5", "Automatic" });

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { "1.5L", "Diesel", "5", "Manual" });

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { "2.0L", "Petrol", "5", "Automatic" });

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { "2.0L", "Diesel", "5", "Automatic" });

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { "1.6L", "Diesel", "5", "Automatic" });

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { "1.0L", "Petrol", "5", "Manual" });

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { "1.0L", "Petrol", "5", "Manual" });

            migrationBuilder.InsertData(
                table: "Cars",
                columns: new[] { "Id", "Category", "Description", "Engine", "FuelType", "ImageUrl", "Model", "PricePerDay", "Seats", "Transmission" },
                values: new object[,]
                {
                    { 8, "Economy", "Efficient and affordable electric sedan with modern technology.", "Electric", "Electric", "/img/tesla-model-3.png", "Tesla Model 3", "60", "5", "Automatic" },
                    { 9, "Standard", "Popular electric hatchback, perfect for city and daily use.", "Electric", "Electric", "/img/nissan-leaf.png", "Nissan Leaf", "55", "5", "Automatic" },
                    { 10, "Luxury", "High-performance luxury electric sedan with cutting-edge features.", "Electric", "Electric", "/img/porsche-taycan.png", "Porsche Taycan", "120", "5", "Automatic" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Engine", "FuelType", "Seats", "Transmission" },
                values: new object[] { null, null, null, null });
        }
    }
}
