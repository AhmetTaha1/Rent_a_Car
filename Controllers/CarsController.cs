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
        private record PendingBooking(int CarId, int UserId, DateTime StartDate, DateTime EndDate, decimal TotalPrice, DateTime CreatedAt);
        private static readonly ConcurrentDictionary<string, PendingBooking> PendingBookings = new();

        // Checkouts the customer abandoned (closed the tab, never paid) are dropped after this long
        // so the in-memory map can't grow forever.
        private static readonly TimeSpan PendingBookingLifetime = TimeSpan.FromHours(1);

        private readonly ILogger<CarsController> _logger;

        public CarsController(DataContext context, IConfiguration config, ILogger<CarsController> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        private static void PruneExpiredPendingBookings()
        {
            var cutoff = DateTime.UtcNow - PendingBookingLifetime;
            foreach (var entry in PendingBookings)
            {
                if (entry.Value.CreatedAt < cutoff)
                    PendingBookings.TryRemove(entry.Key, out _);
            }
        }

        /// <summary>
        /// Refunds a completed iyzico payment in full. Returns false (and logs why) if iyzico
        /// rejects the refund or can't be reached, so callers never tell a customer they've been
        /// refunded when they haven't.
        /// </summary>
        private async Task<bool> TryRefund(string paymentTransactionId, decimal amount, string? currency)
        {
            try
            {
                var refund = await Refund.Create(new CreateRefundRequest
                {
                    ConversationId = Guid.NewGuid().ToString("N"),
                    PaymentTransactionId = paymentTransactionId,
                    Price = amount.ToString(CultureInfo.InvariantCulture),
                    Currency = currency ?? _config["Iyzico:Currency"] ?? Currency.TRY.ToString(),
                    Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1"
                }, GetIyzicoOptions());

                if (refund.Status == "success")
                    return true;

                _logger.LogError("iyzico refund rejected for transaction {TransactionId}: {Error}",
                    paymentTransactionId, refund.ErrorMessage);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iyzico refund failed for transaction {TransactionId}", paymentTransactionId);
                return false;
            }
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

            PruneExpiredPendingBookings();
            var conversationId = Guid.NewGuid().ToString("N");
            PendingBookings[conversationId] = new PendingBooking(carId, userId.Value, start, end, totalPrice, DateTime.UtcNow);

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
                _logger.LogError(ex, "iyzico checkout form initialization failed");
                TempData["SwalError"] = "Payment could not be started. Please try again later.";
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
                    var transactionId = checkoutForm.PaymentItems?.FirstOrDefault()?.PaymentTransactionId;

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
                            Status = ReservationStatus.Confirmed,
                            PaymentId = checkoutForm.PaymentId,
                            PaymentTransactionId = transactionId
                        });
                        await _context.SaveChangesAsync();

                        TempData["SwalSuccess"] = "Payment successful! Your reservation has been confirmed.";
                        redirectUrl = Url.Action("MyReservations", "Cars")!;
                    }
                    else
                    {
                        // Charged them but can't honor the booking - refund immediately rather
                        // than leaving a customer paid with nothing to show for it.
                        var refunded = !string.IsNullOrEmpty(transactionId)
                            && await TryRefund(transactionId, pending.TotalPrice, checkoutForm.Currency);

                        if (refunded)
                        {
                            TempData["SwalError"] = "This car was booked by someone else while your payment was processing. You have been refunded.";
                        }
                        else
                        {
                            _logger.LogCritical("Payment {PaymentId} could not be honored or refunded automatically; manual refund required",
                                checkoutForm.PaymentId);
                            TempData["SwalError"] = "This car was booked by someone else while your payment was processing. " +
                                "We couldn't refund you automatically - our team has been notified and will refund you manually.";
                        }
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
                _logger.LogError(ex, "iyzico payment verification failed");
                TempData["SwalError"] = "Payment verification failed. If you were charged, please contact us.";
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

            if (reservation.Status != ReservationStatus.Confirmed)
                return RedirectToAction("MyReservations");

            // Refund first, cancel second: if the refund fails the booking stays intact, so the
            // customer never ends up with neither their money nor their car.
            if (!string.IsNullOrEmpty(reservation.PaymentTransactionId))
            {
                if (!await TryRefund(reservation.PaymentTransactionId, reservation.TotalPrice, null))
                {
                    TempData["SwalError"] = "Your refund could not be processed, so the reservation was not cancelled. Please try again later.";
                    return RedirectToAction("MyReservations");
                }
                reservation.RefundedAt = DateTime.UtcNow;
            }

            reservation.Status = ReservationStatus.Cancelled;
            await _context.SaveChangesAsync();

            TempData["SwalSuccess"] = reservation.RefundedAt != null
                ? $"Reservation cancelled. ${reservation.TotalPrice:0.##} has been refunded to your card."
                : "Reservation cancelled.";
            return RedirectToAction("MyReservations");
        }
    }
}
