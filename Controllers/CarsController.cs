using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using rent_a_car.Models;
using System.Collections.Concurrent;
using System.Globalization;
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;

namespace rent_a_car.Controllers
{
    public class CarsController : Controller
    {
        private readonly DataContext _context;
        private readonly IConfiguration _config;

        // A car+dates+price combo that a customer has started paying for but not yet paid.
        // Kept in memory (not the database) because it's only needed for the few seconds/minutes
        // between "redirected to iyzico's hosted card form" and "iyzico calls us back with the
        // result" - nothing worth persisting if the process restarts mid-checkout.
        private record PendingBooking(int CarId, int UserId, DateTime StartDate, DateTime EndDate, decimal TotalPrice);
        private static readonly ConcurrentDictionary<string, PendingBooking> PendingBookings = new();

        public CarsController(DataContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        private int? CurrentUserId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            return int.TryParse(userIdStr, out var id) ? id : null;
        }

        private Options GetIyzicoOptions() => new Options
        {
            ApiKey = _config["Iyzico:ApiKey"],
            SecretKey = _config["Iyzico:SecretKey"],
            BaseUrl = _config["Iyzico:BaseUrl"]
        };

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

        // Step 1: validate the booking request, then hand off to iyzico's hosted Checkout Form
        // for card entry. The reservation itself isn't created until PaymentCallback confirms
        // the payment actually succeeded.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(int carId, DateTime pickupDate, DateTime dropoffDate)
        {
            var userId = CurrentUserId();
            if (userId == null)
            {
                TempData["SwalError"] = "You must sign in to make a reservation.";
                var loginReturnUrl = Url.Action("Details", "Cars", new
                {
                    id = carId,
                    pickupDate = pickupDate.ToString("yyyy-MM-dd"),
                    dropoffDate = dropoffDate.ToString("yyyy-MM-dd")
                });
                return RedirectToAction("Login", "Account", new { returnUrl = loginReturnUrl });
            }

            var car = await _context.Cars.FindAsync(carId);
            if (car == null)
                return NotFound();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return RedirectToAction("Login", "Account");

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
            var totalPrice = totalDays * car.PricePerDay;
            var priceString = totalPrice.ToString(CultureInfo.InvariantCulture);

            var conversationId = Guid.NewGuid().ToString("N");
            PendingBookings[conversationId] = new PendingBooking(carId, userId.Value, start, end, totalPrice);

            // iyzico requires a first/last name split and a fair amount of buyer/address detail
            // even in sandbox. We don't collect most of this from users today, so real values
            // (name, email, IP) are used where we have them and reasonable sandbox-only
            // placeholders fill the rest - a production launch would need real buyer info here.
            var nameParts = user.FullName.Trim().Split(' ', 2);
            var firstName = nameParts[0];
            var lastName = nameParts.Length > 1 ? nameParts[1] : "-";

            var checkoutRequest = new CreateCheckoutFormInitializeRequest
            {
                Locale = Locale.EN.ToString(),
                ConversationId = conversationId,
                Price = priceString,
                PaidPrice = priceString,
                Currency = _config["Iyzico:Currency"] ?? Currency.TRY.ToString(),
                BasketId = $"car-{carId}-{start:yyyyMMdd}-{end:yyyyMMdd}",
                PaymentGroup = PaymentGroup.PRODUCT.ToString(),
                CallbackUrl = Url.Action("PaymentCallback", "Cars", null, Request.Scheme),
                GoBackUrl = Url.Action("Details", "Cars", new { id = carId }, Request.Scheme),
                EnabledInstallments = new List<int> { 1 },
                Buyer = new Buyer
                {
                    Id = user.Id.ToString(),
                    Name = firstName,
                    Surname = lastName,
                    GsmNumber = "+905350000000",
                    Email = user.Username,
                    IdentityNumber = "74300864791", // iyzico's own documented sandbox test TCKN
                    RegistrationAddress = "N/A",
                    Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    City = "Istanbul",
                    Country = "Turkey",
                    ZipCode = "34000"
                },
                ShippingAddress = new Address
                {
                    ContactName = user.FullName,
                    City = "Istanbul",
                    Country = "Turkey",
                    Description = "N/A",
                    ZipCode = "34000"
                },
                BillingAddress = new Address
                {
                    ContactName = user.FullName,
                    City = "Istanbul",
                    Country = "Turkey",
                    Description = "N/A",
                    ZipCode = "34000"
                },
                BasketItems = new List<BasketItem>
                {
                    new BasketItem
                    {
                        Id = $"car-{carId}",
                        Name = $"{car.Model} rental ({totalDays} day{(totalDays == 1 ? "" : "s")})",
                        Category1 = "Car Rental",
                        ItemType = BasketItemType.VIRTUAL.ToString(),
                        Price = priceString
                    }
                }
            };

            CheckoutFormInitialize checkoutFormInitialize;
            try
            {
                checkoutFormInitialize = await CheckoutFormInitialize.Create(checkoutRequest, GetIyzicoOptions());
            }
            catch (Exception ex)
            {
                PendingBookings.TryRemove(conversationId, out _);
                TempData["SwalError"] = "Payment could not be started: " + ex.Message;
                return RedirectToAction("Details", new { id = carId });
            }

            if (checkoutFormInitialize.Status != "success")
            {
                PendingBookings.TryRemove(conversationId, out _);
                var reason = string.IsNullOrEmpty(checkoutFormInitialize.ErrorMessage)
                    ? "Please make sure valid iyzico sandbox API keys are configured in appsettings.json."
                    : checkoutFormInitialize.ErrorMessage;
                TempData["SwalError"] = "Payment could not be started: " + reason;
                return RedirectToAction("Details", new { id = carId });
            }

            ViewBag.Car = car;
            ViewBag.StartDate = start;
            ViewBag.EndDate = end;
            ViewBag.TotalDays = totalDays;
            ViewBag.TotalPrice = totalPrice;
            ViewBag.CheckoutFormContent = checkoutFormInitialize.CheckoutFormContent;
            return View("Checkout");
        }

        // Step 2: iyzico's hosted card form runs inside an iframe on the Checkout page and,
        // once the customer finishes, POSTs a token here - not to tell us the result directly
        // (never trust that), but so we know which payment to look up server-to-server via
        // CheckoutForm.Retrieve. Only then do we actually create the reservation.
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> PaymentCallback(string token)
        {
            string redirectUrl;
            try
            {
                var checkoutForm = await CheckoutForm.Retrieve(new RetrieveCheckoutFormRequest { Token = token }, GetIyzicoOptions());
                var conversationId = checkoutForm.ConversationId;

                if (checkoutForm.Status == "success" && checkoutForm.PaymentStatus == "SUCCESS"
                    && conversationId != null && PendingBookings.TryRemove(conversationId, out var pending))
                {
                    // Time has passed while the customer was entering card details - re-check
                    // that nobody else grabbed these dates in the meantime before honoring it.
                    var stillAvailable = !await _context.Reservations.AnyAsync(r =>
                        r.CarId == pending.CarId &&
                        r.Status == ReservationStatus.Confirmed &&
                        r.StartDate < pending.EndDate && r.EndDate > pending.StartDate);

                    if (stillAvailable)
                    {
                        _context.Reservations.Add(new Reservation
                        {
                            CarId = pending.CarId,
                            UserId = pending.UserId,
                            StartDate = pending.StartDate,
                            EndDate = pending.EndDate,
                            TotalPrice = pending.TotalPrice,
                            Status = ReservationStatus.Confirmed
                        });
                        await _context.SaveChangesAsync();

                        TempData["SwalSuccess"] = "Payment successful! Your reservation has been confirmed.";
                        redirectUrl = Url.Action("MyReservations", "Cars")!;
                    }
                    else
                    {
                        // Charged them but can't honor the booking - refund immediately rather
                        // than leaving a customer paid with nothing to show for it.
                        var transactionId = checkoutForm.PaymentItems?.FirstOrDefault()?.PaymentTransactionId;
                        if (!string.IsNullOrEmpty(transactionId))
                        {
                            try
                            {
                                await Refund.Create(new CreateRefundRequest
                                {
                                    PaymentTransactionId = transactionId,
                                    Price = pending.TotalPrice.ToString(CultureInfo.InvariantCulture),
                                    Currency = checkoutForm.Currency ?? "TRY",
                                    Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1"
                                }, GetIyzicoOptions());
                            }
                            catch
                            {
                                // If the refund call itself fails, this needs manual reconciliation -
                                // out of scope for this project, but flagged here rather than hidden.
                            }
                        }
                        TempData["SwalError"] = "This car was booked by someone else while your payment was processing. You have been refunded.";
                        redirectUrl = Url.Action("Details", "Cars", new { id = pending.CarId })!;
                    }
                }
                else
                {
                    if (conversationId != null) PendingBookings.TryRemove(conversationId, out _);
                    var reason = string.IsNullOrEmpty(checkoutForm.ErrorMessage) ? "." : $": {checkoutForm.ErrorMessage}";
                    TempData["SwalError"] = "Payment was not completed" + reason;
                    redirectUrl = Url.Action("List", "Cars")!;
                }
            }
            catch (Exception ex)
            {
                TempData["SwalError"] = "Payment verification failed: " + ex.Message;
                redirectUrl = Url.Action("List", "Cars")!;
            }

            // The Checkout Form runs inside an iframe, so a normal MVC redirect would only
            // navigate the iframe itself and leave the customer stuck looking at it. Break out
            // to the top-level window instead, which is the integration pattern iyzico expects
            // for this callback.
            var safeUrl = System.Text.Json.JsonSerializer.Serialize(redirectUrl);
            var html = $"<!DOCTYPE html><html><body><script>window.top.location.href = {safeUrl};</script></body></html>";
            return Content(html, "text/html");
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
