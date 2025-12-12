using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using rent_a_car.Models;

namespace rent_a_car.Controllers
{
    public class CarsController : Controller
    {
        private readonly DataContext _context;

        public CarsController(DataContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> List(DateTime? pickupDate, DateTime? dropoffDate, string? category)
        {
            // Tarihler zorunlu
            if (!pickupDate.HasValue || !dropoffDate.HasValue)
            {
                ViewBag.PickupDate = pickupDate?.ToString("yyyy-MM-dd");
                ViewBag.DropoffDate = dropoffDate?.ToString("yyyy-MM-dd");
                ViewBag.SelectedCategory = category;
                ViewBag.Error = "Lütfen hem teslim alma hem de bırakma tarihi seçiniz.";
                return View(new List<Car>());
            }

            var cars = await _context.Cars.ToListAsync();

            // Kategori filtrelemesi
            if (!string.IsNullOrEmpty(category))
            {
                cars = cars.Where(c => c.Category.ToLower() == category.ToLower()).ToList();
            }

            // İleride tarih kontrolü yapılacaksa burada yapılabilir

            ViewBag.PickupDate = pickupDate?.ToString("yyyy-MM-dd");
            ViewBag.DropoffDate = dropoffDate?.ToString("yyyy-MM-dd");
            ViewBag.SelectedCategory = category;

            return View(cars);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var car = await _context.Cars.FirstOrDefaultAsync(c => c.Id == id);
            if (car == null)
                return NotFound();

            var similarCars = await _context.Cars
                .Where(c => c.Category == car.Category && c.Id != id)
                .Take(4)
                .ToListAsync();

            ViewBag.SimilarCars = similarCars;
            return View(car);
        }
    }
}
