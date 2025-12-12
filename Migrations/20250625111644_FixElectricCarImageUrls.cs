using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace rent_a_car.Migrations
{
    /// <inheritdoc />
    public partial class FixElectricCarImageUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 8,
                column: "ImageUrl",
                value: "/img/teslamodel3.webp");

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 9,
                column: "ImageUrl",
                value: "/img/nissanleaf.webp");

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 10,
                column: "ImageUrl",
                value: "/img/taycan.webp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 8,
                column: "ImageUrl",
                value: "/img/tesla-model-3.png");

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 9,
                column: "ImageUrl",
                value: "/img/nissan-leaf.png");

            migrationBuilder.UpdateData(
                table: "Cars",
                keyColumn: "Id",
                keyValue: 10,
                column: "ImageUrl",
                value: "/img/porsche-taycan.png");
        }
    }
}
