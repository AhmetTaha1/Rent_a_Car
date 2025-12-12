using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace rent_a_car.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: false),
                    PricePerDay = table.Column<string>(type: "TEXT", nullable: false),
                    Transmission = table.Column<string>(type: "TEXT", nullable: true),
                    Engine = table.Column<string>(type: "TEXT", nullable: true),
                    FuelType = table.Column<string>(type: "TEXT", nullable: true),
                    Seats = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cars", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Cars",
                columns: new[] { "Id", "Category", "Description", "Engine", "FuelType", "ImageUrl", "Model", "PricePerDay", "Seats", "Transmission" },
                values: new object[,]
                {
                    { 1, "Economy", "Compact and fuel-efficient, perfect for city driving.", null, null, "/img/opel-corsa-54390.webp", "Opel Corsa", "35", null, null },
                    { 2, "Standard", "Reliable and comfortable sedan, great for daily use.", null, null, "/img/astra-54407.webp", "Opel Astra", "45", null, null },
                    { 3, "Luxury", "Powerful sports car with impressive performance and style.", null, null, "/img/mercedes-c-200-48693.webp", "Mercedes C200", "65", null, null },
                    { 4, "Luxury", "Powerful sports car with impressive performance and style.", null, null, "/img/320i-56645.webp", "BMW 320i", "75", null, null },
                    { 5, "SUV", "Spacious and versatile SUV, perfect for family trips.", null, null, "/img/nwqash-64376.webp", "Nissan Qashqai", "55", null, null },
                    { 6, "SUV", "Compact crossover with excellent fuel economy.", null, null, "/img/bayon.webp", "Hyundai Bayon", "45", null, null },
                    { 7, "Economy", "Stylish and agile hatchback, ideal for city driving.", null, null, "/img/clio2024-55893.webp", "Renault Clio", "30", null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cars");
        }
    }
}
