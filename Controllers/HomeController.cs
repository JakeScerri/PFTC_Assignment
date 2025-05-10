using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using JakeScerriPFTC_Assignment.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace JakeScerriPFTC_Assignment.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [Authorize]
    public IActionResult UserArea()
    {
        return View();
    }

    [Authorize(Roles = "Technician")]
    public IActionResult TechnicianArea()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}