using Microsoft.AspNetCore.Mvc;
using rent_a_car.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace rent_a_car.Controllers;

public class AdminController : Controller
{
    private readonly DataContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AdminController> _logger;

    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

    // Admin-uploaded car photos live here, separate from the site's own static images in
    // wwwroot/img, so they can be git-ignored and safely deleted when a car's photo changes.
    private const string UploadFolder = "uploads/cars";

    public AdminController(DataContext context, IWebHostEnvironment env, ILogger<AdminController> logger)
    {
        _context = context;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Validates the uploaded image's extension and size before it ever touches disk.
    /// </summary>
    private bool IsValidImageUpload(IFormFile file, out string? error)
    {
        error = null;
        var ext = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
        {
            error = "Only jpg, jpeg, png, webp, or gif files are allowed.";
            return false;
        }
        if (file.Length > MaxImageSizeBytes)
        {
            error = "Image size must not exceed 5 MB.";
            return false;
        }
        return true;
    }

    private async Task<string> SaveUploadedImage(IFormFile file)
    {
        var folder = Path.Combine(_env.WebRootPath, UploadFolder);
        Directory.CreateDirectory(folder);

        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName).ToLowerInvariant();
        using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
        return $"/{UploadFolder}/{fileName}";
    }

    /// <summary>
    /// Deletes a previously uploaded car photo. Only touches files inside the uploads folder,
    /// never the bundled seed images in wwwroot/img.
    /// </summary>
    private void DeleteUploadedImage(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl) || !imageUrl.StartsWith($"/{UploadFolder}/")) return;

        var path = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/'));
        try
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not delete old car image {Path}", path);
        }
    }

    /// <summary>
    /// Returns null if the current session belongs to a user who is an admin *right now*
    /// according to the database, otherwise the redirect to send them to. Checking the database
    /// on every admin request (instead of trusting the IsAdmin flag cached in the session)
    /// means revoking someone's admin rights takes effect immediately, even across restarts.
    /// </summary>
    private async Task<IActionResult?> AdminOnly()
    {
        var userIdStr = HttpContext.Session.GetString("UserId");
        if (!int.TryParse(userIdStr, out var userId))
            return RedirectToAction("Login", "Account");

        var user = await _context.Users.FindAsync(userId);
        if (user != null && user.IsAdmin)
            return null;

        if (HttpContext.Session.GetString("IsAdmin") == "true")
        {
            // Session still thinks they're an admin, but they've been demoted (or deleted).
            HttpContext.Session.Clear();
            TempData["SwalInfo"] = "You are no longer an admin. Your session has been ended.";
            return RedirectToAction("Index", "Home");
        }
        return RedirectToAction("Login", "Account");
    }

    private int CurrentUserId() => int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : 0;

    public async Task<IActionResult> Dashboard()
    {
        var result = await AdminOnly();
        if (result != null) return result;

        return View();
    }

    public async Task<IActionResult> CarCreate()
    {
        var result = await AdminOnly();
        if (result != null) return result;

        return View();
    }

    public async Task<IActionResult> CarList()
    {
        var result = await AdminOnly();
        if (result != null) return result;

        var cars = await _context.Cars.ToListAsync();
        return View(cars);
    }

    // List all reservations
    public async Task<IActionResult> ReservationList()
    {
        var result = await AdminOnly();
        if (result != null) return result;

        var reservations = await _context.Reservations
            .Include(r => r.Car)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return View(reservations);
    }

    // List all users
    public async Task<IActionResult> UserList()
    {
        var result = await AdminOnly();
        if (result != null) return result;

        var users = await _context.Users.ToListAsync();
        return View(users);
    }

    // Grant admin rights. Only the primary admin (Id = 1) may do this.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeAdmin(int id)
    {
        var result = await AdminOnly();
        if (result != null) return result;

        if (CurrentUserId() != 1)
        {
            TempData["SwalError"] = "Only the primary admin can grant admin rights.";
            return RedirectToAction("UserList");
        }

        var user = await _context.Users.FindAsync(id);
        if (user != null && !user.IsAdmin)
        {
            user.IsAdmin = true;
            await _context.SaveChangesAsync();
            TempData["SwalSuccess"] = $"{user.FullName} is now an admin.";
        }
        return RedirectToAction("UserList");
    }

    // Revoke admin rights. Only the primary admin (Id = 1) may do this, and they can't demote themselves.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAdmin(int id)
    {
        var result = await AdminOnly();
        if (result != null) return result;

        if (CurrentUserId() != 1)
        {
            TempData["SwalError"] = "Only the primary admin can revoke admin rights.";
            return RedirectToAction("UserList");
        }

        var user = await _context.Users.FindAsync(id);
        if (user != null && user.IsAdmin && user.Id != 1)
        {
            user.IsAdmin = false;
            await _context.SaveChangesAsync();
            TempData["SwalSuccess"] = $"{user.FullName} is no longer an admin.";
        }
        return RedirectToAction("UserList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CarCreate(Car car, IFormFile? imageFile)
    {
        var result = await AdminOnly();
        if (result != null) return result;

        // ImageUrl is derived from the uploaded file below, not bound directly from the form;
        // clear any implicit "required" error so our specific validation message is the one shown.
        ModelState.Remove(nameof(Car.ImageUrl));

        if (imageFile == null || imageFile.Length == 0)
            ModelState.AddModelError("ImageUrl", "Image is required.");
        else if (!IsValidImageUpload(imageFile, out var uploadError))
            ModelState.AddModelError("ImageUrl", uploadError!);

        if (!ModelState.IsValid)
            return View(car);

        car.ImageUrl = await SaveUploadedImage(imageFile!);
        _context.Cars.Add(car);
        await _context.SaveChangesAsync();

        TempData["SwalSuccess"] = "Car added successfully.";
        return RedirectToAction("CarList");
    }

    [HttpGet]
    public async Task<IActionResult> CarEdit(int id)
    {
        var result = await AdminOnly();
        if (result != null) return result;

        var car = await _context.Cars.FindAsync(id);
        if (car == null) return NotFound();
        return View(car);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CarEdit(int id, Car updatedCar, IFormFile? imageFile)
    {
        var result = await AdminOnly();
        if (result != null) return result;

        var car = await _context.Cars.FindAsync(id);
        if (car == null) return NotFound();

        // updatedCar.ImageUrl is never posted from this form; clear its implicit "required" error
        // so a real upload-validation message (if any) is the one displayed.
        ModelState.Remove(nameof(Car.ImageUrl));

        if (imageFile != null && imageFile.Length > 0 && !IsValidImageUpload(imageFile, out var uploadError))
            ModelState.AddModelError("ImageUrl", uploadError!);

        if (!ModelState.IsValid)
        {
            // Re-render with what the admin typed, but keep the current photo visible.
            updatedCar.Id = car.Id;
            updatedCar.ImageUrl = car.ImageUrl;
            return View(updatedCar);
        }

        // Save the new photo (if any) and remove the one it replaces
        if (imageFile != null && imageFile.Length > 0)
        {
            var oldImageUrl = car.ImageUrl;
            car.ImageUrl = await SaveUploadedImage(imageFile);
            DeleteUploadedImage(oldImageUrl);
        }

        // Update the remaining fields
        car.Model = updatedCar.Model;
        car.Category = updatedCar.Category;
        car.Description = updatedCar.Description;
        car.PricePerDay = updatedCar.PricePerDay;
        car.Transmission = updatedCar.Transmission;
        car.Engine = updatedCar.Engine;
        car.FuelType = updatedCar.FuelType;
        car.Seats = updatedCar.Seats;

        await _context.SaveChangesAsync();

        TempData["SwalSuccess"] = "Car updated successfully.";
        return RedirectToAction("CarList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CarDelete(int id)
    {
        var result = await AdminOnly();
        if (result != null) return result;

        var car = await _context.Cars.FindAsync(id);
        if (car == null)
        {
            TempData["SwalError"] = "Car not found.";
            return RedirectToAction("CarList");
        }

        // Reservations are paid records - deleting the car must never silently wipe them out.
        if (await _context.Reservations.AnyAsync(r => r.CarId == id))
        {
            TempData["SwalError"] = "This car has reservations on record and can't be deleted.";
            return RedirectToAction("CarList");
        }

        _context.Cars.Remove(car);
        await _context.SaveChangesAsync();
        DeleteUploadedImage(car.ImageUrl);

        TempData["SwalSuccess"] = "Car deleted successfully.";
        return RedirectToAction("CarList");
    }
}
