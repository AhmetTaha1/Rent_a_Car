using System.ComponentModel.DataAnnotations;

namespace rent_a_car.Models;

public class Car
{
    public int Id { get; set; }
    public string Model { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string? Description { get; set; }
    public string ImageUrl { get; set; } = null!;
    [Range(typeof(decimal), "1", "100000", ErrorMessage = "Price per day must be between 1 and 100000.")]
    public decimal PricePerDay { get; set; }
    public string? Transmission { get; set; }
    public string? Engine { get; set; }
    public string? FuelType { get; set; }
    public string? Seats { get; set; }


}