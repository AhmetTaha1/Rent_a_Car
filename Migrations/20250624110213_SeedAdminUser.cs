using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace rent_a_car.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdminUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "FullName", "IsAdmin", "PasswordHash", "Username" },
                values: new object[] { 1, "Admin User", true, "n4bQgWbL8zA1Q9Qf8rQJvQw8Qw8Qw8Qw8Qw8Qw8Qw8Qw=", "admin@site.com" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
