using Microsoft.AspNetCore.Mvc;
using rent_a_car.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace rent_a_car.Controllers;

public class AccountController : Controller
{
    private readonly DataContext _context;
    public AccountController(DataContext context)
    {
        _context = context;
    }

    [HttpGet]
    public ActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SignIn()
    {
        var email = Request.Form["Email"].ToString();
        var password = Request.Form["Password"].ToString();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError("", "Email ve şifre gereklidir.");
            return View("Login");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == email);
        if (user == null)
        {
            ModelState.AddModelError("", "Kullanıcı bulunamadı.");
            return View("Login");
        }
        if (!VerifyPasswordHash(password, user.PasswordHash))
        {
            ModelState.AddModelError("", "Şifre yanlış.");
            return View("Login");
        }
        // Oturum aç
        HttpContext.Session.SetString("UserId", user.Id.ToString());
        HttpContext.Session.SetString("FullName", user.FullName);
        HttpContext.Session.SetString("IsAdmin", user.IsAdmin ? "true" : "false");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public ActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SignUp(string FullName, string Email, string Password, string ConfirmPassword)
    {
        if (Password != ConfirmPassword)
        {
            ModelState.AddModelError("", "Passwords do not match.");
            return View("Register");
        }
        if (await _context.Users.AnyAsync(u => u.Username == Email))
        {
            ModelState.AddModelError("", "Email already registered.");
            return View("Register");
        }
        var user = new User
        {
            Username = Email,
            FullName = FullName,
            PasswordHash = HashPassword(Password),
            IsAdmin = false
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        // Oturum aç
        HttpContext.Session.SetString("UserId", user.Id.ToString());
        HttpContext.Session.SetString("FullName", user.FullName);
        HttpContext.Session.SetString("IsAdmin", user.IsAdmin ? "true" : "false");
        return RedirectToAction("Index", "Home");
    }

    private string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }

    private bool VerifyPasswordHash(string password, string storedHash)
    {
        var hashOfInput = HashPassword(password);
        Console.WriteLine($"Girdi: {password} | Hash: {hashOfInput} | DB Hash: {storedHash}");
        return hashOfInput == storedHash;
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }
}
