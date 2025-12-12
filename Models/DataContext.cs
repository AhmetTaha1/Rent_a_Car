using Microsoft.EntityFrameworkCore;

namespace rent_a_car.Models
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }

        public DbSet<Car> Cars { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Car>().HasData(
                new Car
                {
                    Id = 1,
                    Model = "Opel Corsa",
                    Category = "Economy",
                    Description = "Compact and fuel-efficient, perfect for city driving.",
                    ImageUrl = "/img/opel-corsa-54390.webp",
                    PricePerDay = "35",
                    FuelType = "Petrol",
                    Transmission = "Automatic",
                    Engine = "1.2L",
                    Seats = "5"
                },
                new Car
                {
                    Id = 2,
                    Model = "Opel Astra",
                    Category = "Standard",
                    Description = "Reliable and comfortable sedan, great for daily use.",
                    ImageUrl = "/img/astra-54407.webp",
                    PricePerDay = "45",
                    FuelType = "Diesel",
                    Transmission = "Manual",
                    Engine = "1.5L",
                    Seats = "5"
                },
                new Car
                {
                    Id = 3,
                    Model = "Mercedes C200",
                    Category = "Luxury",
                    Description = "Powerful sports car with impressive performance and style.",
                    ImageUrl = "/img/mercedes-c-200-48693.webp",
                    PricePerDay = "65",
                    FuelType = "Petrol",
                    Transmission = "Automatic",
                    Engine = "2.0L",
                    Seats = "5"
                },
                new Car
                {
                    Id = 4,
                    Model = "BMW 320i",
                    Category = "Luxury",
                    Description = "Powerful sports car with impressive performance and style.",
                    ImageUrl = "/img/320i-56645.webp",
                    PricePerDay = "75",
                    FuelType = "Diesel",
                    Transmission = "Automatic",
                    Engine = "2.0L",
                    Seats = "5"
                },
                new Car
                {
                    Id = 5,
                    Model = "Nissan Qashqai",
                    Category = "SUV",
                    Description = "Spacious and versatile SUV, perfect for family trips.",
                    ImageUrl = "/img/nwqash-64376.webp",
                    PricePerDay = "55",
                    FuelType = "Diesel",
                    Transmission = "Automatic",
                    Engine = "1.6L",
                    Seats = "5"
                },
                new Car
                {
                    Id = 6,
                    Model = "Hyundai Bayon",
                    Category = "SUV",
                    Description = "Compact crossover with excellent fuel economy.",
                    ImageUrl = "/img/bayon.webp",
                    PricePerDay = "45",
                    FuelType = "Petrol",
                    Transmission = "Manual",
                    Engine = "1.0L",
                    Seats = "5"
                },
                new Car
                {
                    Id = 7,
                    Model = "Renault Clio",
                    Category = "Economy",
                    Description = "Stylish and agile hatchback, ideal for city driving.",
                    ImageUrl = "/img/clio2024-55893.webp",
                    PricePerDay = "30",
                    FuelType = "Petrol",
                    Transmission = "Manual",
                    Engine = "1.0L",
                    Seats = "5"
                },
                new Car
                {
                    Id = 8,
                    Model = "Tesla Model 3",
                    Category = "Economy",
                    Description = "Efficient and affordable electric sedan with modern technology.",
                    ImageUrl = "/img/teslamodel3.webp",
                    PricePerDay = "60",
                    FuelType = "Electric",
                    Transmission = "Automatic",
                    Engine = "Electric",
                    Seats = "5"
                },
                new Car
                {
                    Id = 9,
                    Model = "Nissan Leaf",
                    Category = "Standard",
                    Description = "Popular electric hatchback, perfect for city and daily use.",
                    ImageUrl = "/img/nissanleaf.webp",
                    PricePerDay = "55",
                    FuelType = "Electric",
                    Transmission = "Automatic",
                    Engine = "Electric",
                    Seats = "5"
                },
                new Car
                {
                    Id = 10,
                    Model = "Porsche Taycan",
                    Category = "Luxury",
                    Description = "High-performance luxury electric sedan with cutting-edge features.",
                    ImageUrl = "/img/taycan.webp",
                    PricePerDay = "120",
                    FuelType = "Electric",
                    Transmission = "Automatic",
                    Engine = "Electric",
                    Seats = "5"
                }
            );

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin@site.com",
                    FullName = "Admin User",
                    PasswordHash = "73l8gRjwLftklgfdXT+MdiMEjJwGPVMsyVxe16iYpk8=", // 'Admin123!' için gerçek SHA256 hash
                    IsAdmin = true
                }
            );
        }
    }
}
