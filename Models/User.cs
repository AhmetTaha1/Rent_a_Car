using System.ComponentModel.DataAnnotations;

namespace rent_a_car.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = null!;

        public bool IsAdmin { get; set; } = false;

        [Required]
        public string FullName { get; set; } = null!;
    }
} 