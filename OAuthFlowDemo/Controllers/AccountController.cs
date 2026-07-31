using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OAuthFlowDemo.Controllers;

[AllowAnonymous]
public sealed class AccountController(IConfiguration configuration) : Controller
{
    [HttpGet("sign-in")]
    public IActionResult SignIn()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        var authMode = configuration.GetValue<string>("AuthMode") ?? "ExternalId";

        if (authMode == "Testing")
        {
            return Redirect("/testing/sign-in/customer");
        }

        return Challenge(new AuthenticationProperties { RedirectUri = "/" }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("sign-out")]
    public new async Task<IActionResult> SignOut()
    {
        var authMode = configuration.GetValue<string>("AuthMode") ?? "ExternalId";

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (authMode != "Testing")
        {
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
                                           new AuthenticationProperties
                                           {
                                               RedirectUri = Url.Action("SignedOut", "Home")
                                           });
        }

        return RedirectToAction("SignedOut", "Home");
    }
}
