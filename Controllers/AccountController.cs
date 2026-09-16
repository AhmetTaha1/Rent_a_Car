using Microsoft.AspNetCore.Mvc;
using rent_a_car.Models;
using rent_a_car.Services;
using Microsoft.EntityFrameworkCore;
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
    public ActionResult Login(string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
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
        if (!PasswordHasher.Verify(password, user.PasswordHash, out var needsUpgrade))
        {
            ModelState.AddModelError("", "Şifre yanlış.");
            return View("Login");
        }
        if (needsUpgrade)
        {
            // Transparently migrate legacy unsalted SHA256 hashes to PBKDF2 on next successful login.
            user.PasswordHash = PasswordHasher.Hash(password);
            await _context.SaveChangesAsync();
        }
        // Oturum aç
        HttpContext.Session.SetString("UserId", user.Id.ToString());
        HttpContext.Session.SetString("FullName", user.FullName);
        HttpContext.Session.SetString("IsAdmin", user.IsAdmin ? "true" : "false");

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public ActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignUp(string FullName, string Email, string Password, string ConfirmPassword)
    {
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ModelState.AddModelError("", "Tüm alanlar zorunludur.");
            return View("Register");
        }
        if (Password.Length < 6)
        {
            ModelState.AddModelError("", "Şifre en az 6 karakter olmalıdır.");
            return View("Register");
        }
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
            PasswordHash = PasswordHasher.Hash(Password),
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

    [HttpPost]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }
}
