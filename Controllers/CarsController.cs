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

        private int? CurrentUserId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            return int.TryParse(userIdStr, out var id) ? id : null;
        }

        public async Task<IActionResult> List(DateTime? pickupDate, DateTime? dropoffDate, string? category)
        {
            // Dates are required
            if (!pickupDate.HasValue || !dropoffDate.HasValue)
            {
                ViewBag.PickupDate = pickupDate?.ToString("yyyy-MM-dd");
                ViewBag.DropoffDate = dropoffDate?.ToString("yyyy-MM-dd");
                ViewBag.SelectedCategory = category;
                ViewBag.Error = "Please select both a pick-up and a drop-off date.";
                return View(new List<Car>());
            }

            if (dropoffDate.Value.Date <= pickupDate.Value.Date)
            {
                ViewBag.PickupDate = pickupDate?.ToString("yyyy-MM-dd");
                ViewBag.DropoffDate = dropoffDate?.ToString("yyyy-MM-dd");
                ViewBag.SelectedCategory = category;
                ViewBag.Error = "The drop-off date must be after the pick-up date.";
                return View(new List<Car>());
            }

            var cars = await _context.Cars.ToListAsync();

            // Category filter
            if (!string.IsNullOrEmpty(category))
            {
                cars = cars.Where(c => c.Category.ToLower() == category.ToLower()).ToList();
            }

            // Exclude cars already reserved for the requested date range
            var start = pickupDate.Value.Date;
            var end = dropoffDate.Value.Date;
            var overlappingCarIds = await _context.Reservations
                .Where(r => r.Status == ReservationStatus.Confirmed && r.StartDate < end && r.EndDate > start)
                .Select(r => r.CarId)
                .Distinct()
                .ToListAsync();

            cars = cars.Where(c => !overlappingCarIds.Contains(c.Id)).ToList();

            ViewBag.PickupDate = pickupDate?.ToString("yyyy-MM-dd");
            ViewBag.DropoffDate = dropoffDate?.ToString("yyyy-MM-dd");
            ViewBag.SelectedCategory = category;

            return View(cars);
        }

        public async Task<IActionResult> Details(int? id, DateTime? pickupDate, DateTime? dropoffDate)
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

            var reservedRanges = await _context.Reservations
                .Where(r => r.CarId == id && r.Status == ReservationStatus.Confirmed && r.EndDate >= DateTime.Today)
                .Select(r => new { start = r.StartDate.ToString("yyyy-MM-dd"), end = r.EndDate.ToString("yyyy-MM-dd") })
                .ToListAsync();

            ViewBag.SimilarCars = similarCars;
            ViewBag.ReservedRanges = reservedRanges;
            ViewBag.PickupDate = pickupDate?.ToString("yyyy-MM-dd");
            ViewBag.DropoffDate = dropoffDate?.ToString("yyyy-MM-dd");
            return View(car);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reserve(int carId, DateTime pickupDate, DateTime dropoffDate)
        {
            var userId = CurrentUserId();
            if (userId == null)
            {
                TempData["SwalError"] = "You must sign in to make a reservation.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Cars", new { id = carId }) });
            }

            var car = await _context.Cars.FindAsync(carId);
            if (car == null)
                return NotFound();

            var start = pickupDate.Date;
            var end = dropoffDate.Date;

            if (end <= start)
            {
                TempData["SwalError"] = "The drop-off date must be after the pick-up date.";
                return RedirectToAction("Details", new { id = carId });
            }
            if (start < DateTime.Today)
            {
                TempData["SwalError"] = "You cannot make a reservation for a past date.";
                return RedirectToAction("Details", new { id = carId });
            }

            var hasOverlap = await _context.Reservations.AnyAsync(r =>
                r.CarId == carId &&
                r.Status == ReservationStatus.Confirmed &&
                r.StartDate < end && r.EndDate > start);

            if (hasOverlap)
            {
                TempData["SwalError"] = "This car is not available for the selected dates.";
                return RedirectToAction("Details", new { id = carId });
            }

            var totalDays = Math.Max(1, (end - start).Days);
            var reservation = new Reservation
            {
                CarId = carId,
                UserId = userId.Value,
                StartDate = start,
                EndDate = end,
                TotalPrice = totalDays * car.PricePerDay,
                Status = ReservationStatus.Confirmed
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var dayLabel = totalDays == 1 ? "day" : "days";
            TempData["SwalSuccess"] = $"Your reservation for {car.Model} has been created. Total: ${reservation.TotalPrice:0.##} ({totalDays} {dayLabel})";
            return RedirectToAction("MyReservations");
        }

        public async Task<IActionResult> MyReservations()
        {
            var userId = CurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("MyReservations", "Cars") });

            var reservations = await _context.Reservations
                .Include(r => r.Car)
                .Where(r => r.UserId == userId.Value)
                .OrderByDescending(r => r.StartDate)
                .ToListAsync();

            return View(reservations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelReservation(int id)
        {
            var userId = CurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var reservation = await _context.Reservations.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId.Value);
            if (reservation == null)
                return NotFound();

            if (reservation.StartDate <= DateTime.Today)
            {
                TempData["SwalError"] = "A reservation that has already started or passed cannot be cancelled.";
                return RedirectToAction("MyReservations");
            }

            reservation.Status = ReservationStatus.Cancelled;
            await _context.SaveChangesAsync();

            TempData["SwalSuccess"] = "Reservation cancelled.";
            return RedirectToAction("MyReservations");
        }
    }
}
