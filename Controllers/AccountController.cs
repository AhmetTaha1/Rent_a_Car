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
            ModelState.AddModelError("", "Email and password are required.");
            return View("Login");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == email);
        if (user == null)
        {
            ModelState.AddModelError("", "User not found.");
            return View("Login");
        }
        if (!PasswordHasher.Verify(password, user.PasswordHash, out var needsUpgrade))
        {
            ModelState.AddModelError("", "Incorrect password.");
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
        TempData["SwalSuccess"] = $"Welcome back, {user.FullName}!";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public ActionResult Register(string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignUp(string FullName, string Email, string Password, string ConfirmPassword, string? returnUrl)
    {
        // Echo back whatever was entered (never the passwords) so a failed attempt doesn't
        // force the user to retype everything - this was silently happening before, with no
        // error shown either, which made sign-up look broken on any validation failure.
        ViewBag.ReturnUrl = returnUrl;
        ViewBag.FullName = FullName;
        ViewBag.Email = Email;

        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ModelState.AddModelError("", "All fields are required.");
            return View("Register");
        }
        if (Password.Length < 6)
        {
            ModelState.AddModelError("", "Password must be at least 6 characters long.");
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
        TempData["SwalSuccess"] = $"Welcome, {user.FullName}! Your account has been created.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }
}
