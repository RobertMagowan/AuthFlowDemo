using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OAuthFlowDemo.Services;

namespace OAuthFlowDemo.Controllers;

[AllowAnonymous]
public sealed class DiagnosticsController(OidcEventStore eventStore) : Controller
{
    [HttpGet("/diagnostics")]
    public IActionResult Index([FromQuery] string format = "html")
    {
        if (format == "json")
        {
            return Json(eventStore.GetAll());
        }

        return View(eventStore.GetAll());
    }

    [HttpGet("/diagnostics/latest")]
    public IActionResult Latest()
    {
        var evt = eventStore.GetLatest();
        if (evt is null)
        {
            return NotFound("No events captured yet.");
        }

        return Json(evt);
    }

    [HttpGet("/diagnostics/count")]
    public IActionResult Count()
    {
        return Content(eventStore.Count.ToString(), "text/plain");
    }

    [HttpDelete("/diagnostics")]
    public IActionResult Clear()
    {
        eventStore.Clear();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/diagnostics/clear")]
    public IActionResult ClearPost()
    {
        eventStore.Clear();
        return RedirectToAction(nameof(Index));
    }
}
