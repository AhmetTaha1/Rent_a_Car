
using Microsoft.AspNetCore.Mvc;

namespace rent_a_car.Controllers;

public class HomeController : Controller
{

    public ActionResult Index()

    {
        return View();

    }

    public ActionResult About()
    {
        ViewData["Title"] = "About Us";
        ViewData["Message"] = "Welcome to Royal Rent!";
        return View();
    }











}
