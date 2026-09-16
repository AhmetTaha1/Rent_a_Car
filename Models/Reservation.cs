using System.ComponentModel.DataAnnotations;

namespace rent_a_car.Models
{
    public enum ReservationStatus
    {
        Confirmed = 0,
        Cancelled = 1
    }

    public class Reservation
    {
        public int Id { get; set; }

        [Required]
        public int CarId { get; set; }
        public Car? Car { get; set; }

        [Required]
        public int UserId { get; set; }
        public User? User { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public decimal TotalPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;
    }
}
