using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OAuthFlowDemo.Controllers;

[AllowAnonymous]
public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("/signed-out")]
    public IActionResult SignedOut()
    {
        return View();
    }

    [HttpGet("/error")]
    public IActionResult Error()
    {
        return View();
    }
}
