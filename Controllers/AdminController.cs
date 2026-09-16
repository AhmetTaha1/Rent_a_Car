using Microsoft.AspNetCore.Mvc;
using rent_a_car.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace rent_a_car.Controllers;

public class AdminController : Controller
{
    private readonly DataContext _context;
    private static ConcurrentDictionary<int, bool> AdminRemovedFlags = new();

    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

    public AdminController(DataContext context)
    {
        _context = context;
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

    private bool IsAdmin()
    {
        // Basit bir session kontrolü (geliştirilebilir)
        return HttpContext.Session.GetString("IsAdmin") == "true";
    }

    private IActionResult? AdminOnly()
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");
        return null;
    }

    public IActionResult Dashboard()
    {
        var result = AdminOnly();
        if (result != null) return result;
        // Adminlikten düşürüldüyse popup ve logout
        if (CheckAndHandleAdminRemoved()) return RedirectToAction("Index", "Home");
        return View();
    }
    public IActionResult CarCreate()
    {
        var result = AdminOnly();
        if (result != null) return result;
        if (CheckAndHandleAdminRemoved()) return RedirectToAction("Index", "Home");
        return View();
    }
    public IActionResult CarEdit()
    {
        var result = AdminOnly();
        if (result != null) return result;
        if (CheckAndHandleAdminRemoved()) return RedirectToAction("Index", "Home");
        return View();
    }
    public async Task<IActionResult> CarList()
    {
        var result = AdminOnly();
        if (result != null) return result;
        if (CheckAndHandleAdminRemoved()) return RedirectToAction("Index", "Home");
        var cars = await _context.Cars.ToListAsync();
        return View(cars);
        
    }

    // List all reservations
    public async Task<IActionResult> ReservationList()
    {
        var result = AdminOnly();
        if (result != null) return result;
        if (CheckAndHandleAdminRemoved()) return RedirectToAction("Index", "Home");
        var reservations = await _context.Reservations
            .Include(r => r.Car)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        return View(reservations);
    }

    // Kullanıcıları listele
    public async Task<IActionResult> UserList()
    {
        var result = AdminOnly();
        if (result != null) return result;
        if (CheckAndHandleAdminRemoved()) return RedirectToAction("Index", "Home");
        var users = await _context.Users.ToListAsync();
        return View(users);
    }

    // Kullanıcıyı admin yap
    [HttpPost]
    public async Task<IActionResult> MakeAdmin(int id)
    {
        var result = AdminOnly();
        if (result != null) return result;
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            // Sadece ilk admin (Id=1) diğerlerini admin yapabilir
            var currentUserId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            if (user.Id == 1) // İlk admini tekrar admin yapmaya gerek yok
                return RedirectToAction("UserList");
            if (currentUserId == 1)
            {
                user.IsAdmin = true;
                await _context.SaveChangesAsync();
            }
        }
        return RedirectToAction("UserList");
    }

    // Kullanıcıyı adminlikten çıkar
    [HttpPost]
    public async Task<IActionResult> RemoveAdmin(int id)
    {
        var result = AdminOnly();
        if (result != null) return result;
        var user = await _context.Users.FindAsync(id);
        if (user != null && user.IsAdmin)
        {
            if (user.Id == 1)
                return RedirectToAction("UserList");
            var currentUserId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            if (currentUserId == 1)
            {
                user.IsAdmin = false;
                await _context.SaveChangesAsync();
                AdminRemovedFlags[user.Id] = true;
            }
        }
        return RedirectToAction("UserList");
    }

    // Adminlikten düşürüldüyse popup ve logout
    private bool CheckAndHandleAdminRemoved()
    {
        var userIdStr = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userIdStr)) return false;
        int userId = int.Parse(userIdStr);
        if (AdminRemovedFlags.TryRemove(userId, out var removed) && removed)
        {
            TempData["SwalInfo"] = "You are no longer an admin. Your session has been ended.";
            HttpContext.Session.Clear();
            return true;
        }
        return false;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CarCreate(Car car, IFormFile? imageFile)
    {
        var result = AdminOnly();
        if (result != null) return result;
        if (CheckAndHandleAdminRemoved()) return RedirectToAction("Index", "Home");

        // ImageUrl is derived from the uploaded file below, not bound directly from the form;
        // clear any implicit "required" error so our specific validation message is the one shown.
        ModelState.Remove(nameof(Car.ImageUrl));

        if (imageFile != null && imageFile.Length > 0)
        {
            if (!IsValidImageUpload(imageFile, out var uploadError))
            {
                ModelState.AddModelError("ImageUrl", uploadError!);
                return View(car);
            }
            var fileName = Guid.NewGuid().ToString() + System.IO.Path.GetExtension(imageFile.FileName);
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/img", fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }
            car.ImageUrl = "/img/" + fileName;
        }
        else
        {
            ModelState.AddModelError("ImageUrl", "Image is required.");
            return View(car);
        }

        _context.Cars.Add(car);
        await _context.SaveChangesAsync();
        return RedirectToAction("CarList");
    }

    [HttpGet]
    public async Task<IActionResult> CarEdit(int id)
    {
        var result = AdminOnly();
        if (result != null) return result;
        if (CheckAndHandleAdminRemoved()) return RedirectToAction("Index", "Home");
        var car = await _context.Cars.FindAsync(id);
        if (car == null) return NotFound();
        return View(car);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CarEdit(int id, Car updatedCar, IFormFile? imageFile, string? delete)
    {
        var result = AdminOnly();
        if (result != null) return result;
        if (CheckAndHandleAdminRemoved()) return RedirectToAction("Index", "Home");
        var car = await _context.Cars.FindAsync(id);
        if (car == null) return NotFound();

        if (!string.IsNullOrEmpty(delete))
        {
            _context.Cars.Remove(car);
            await _context.SaveChangesAsync();
            return RedirectToAction("CarList");
        }

        // updatedCar.ImageUrl is never posted from this form; clear its implicit "required" error
        // so a real upload-validation message (if any) is the one displayed.
        ModelState.Remove(nameof(Car.ImageUrl));

        // Fotoğraf değiştiyse yeni fotoğrafı kaydet
        if (imageFile != null && imageFile.Length > 0)
        {
            if (!IsValidImageUpload(imageFile, out var uploadError))
            {
                ModelState.AddModelError("ImageUrl", uploadError!);
                return View(car);
            }
            var fileName = Guid.NewGuid().ToString() + System.IO.Path.GetExtension(imageFile.FileName);
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/img", fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }
            car.ImageUrl = "/img/" + fileName;
        }

        // Diğer alanları güncelle
        car.Model = updatedCar.Model;
        car.Category = updatedCar.Category;
        car.Description = updatedCar.Description;
        car.PricePerDay = updatedCar.PricePerDay;
        car.Transmission = updatedCar.Transmission;
        car.Engine = updatedCar.Engine;
        car.FuelType = updatedCar.FuelType;
        car.Seats = updatedCar.Seats;

        await _context.SaveChangesAsync();
        return RedirectToAction("CarList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CarDelete(int id)
    { 
        try
        {
            var result = AdminOnly();
            if (result != null) return RedirectToAction("CarList");
            if (CheckAndHandleAdminRemoved()) return RedirectToAction("Index", "Home");
            var car = await _context.Cars.FindAsync(id);
            if (car == null)
            {
                TempData["SwalError"] = "Car not found.";
                return RedirectToAction("CarList");
            }
            _context.Cars.Remove(car);
            await _context.SaveChangesAsync();
            TempData["SwalSuccess"] = "Car deleted successfully.";
            return RedirectToAction("CarList");
        }
        catch (Exception ex)
        {
            TempData["SwalError"] = "An error occurred while deleting: " + ex.Message;
            return RedirectToAction("CarList");
        }
    }
}