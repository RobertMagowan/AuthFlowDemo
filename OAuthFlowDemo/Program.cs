using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using OAuthFlowDemo.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<OidcEventStore>();
builder.Services.AddHttpContextAccessor();

var authMode = builder.Configuration.GetValue<string>("AuthMode") ?? "ExternalId";

if (authMode == "Testing")
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
           .AddCookie(options =>
           {
               options.Cookie.HttpOnly = true;
               options.ExpireTimeSpan = TimeSpan.FromHours(1);
               options.SlidingExpiration = true;
               options.LoginPath = "/sign-in";
               options.LogoutPath = "/sign-out";
               options.AccessDeniedPath = "/error";

               options.Events.OnSignedIn = context =>
               {
                   var store = context.HttpContext.RequestServices.GetRequiredService<OidcEventStore>();
                    store.Add(new OidcEvent
                    {
                         EventType = "Cookie: OnSignedIn",
                         Description = "Fires after the auth cookie is written to the response. The cookie now contains your claims and will be sent on every subsequent request.",
                         Category = EventCategory.Infrastructure,
                         FlowPhase = "SignIn",
                       Details = new Dictionary<string, string>
                       {
                           ["CookieName"] = ".AspNetCore.Cookies",
                           ["AuthScheme"] = context.Principal?.Identity?.AuthenticationType ?? "unknown",
                           ["Name"] = context.Principal?.Identity?.Name ?? "unknown",
                           ["ClaimsCount"] = context.Principal?.Claims.Count().ToString() ?? "0",
                       }
                   });
                   return Task.CompletedTask;
               };

               options.Events.OnValidatePrincipal = context =>
               {
                   var store = context.HttpContext.RequestServices.GetRequiredService<OidcEventStore>();
                    store.Add(new OidcEvent
                    {
                         EventType = "Cookie: OnValidatePrincipal",
                         Description = "Fires on every request where a valid cookie is present. The middleware decrypts it and restores your identity — how ASP.NET Core knows who you are between requests.",
                         Category = EventCategory.Infrastructure,
                         FlowPhase = "Request",
                       RequestPath = context.HttpContext.Request.Path,
                       Details = new Dictionary<string, string>
                       {
                           ["CookiePresent"] = "true",
                           ["AuthScheme"] = context.Principal?.Identity?.AuthenticationType ?? "unknown",
                           ["Name"] = context.Principal?.Identity?.Name ?? "unknown",
                           ["IsAuthenticated"] = (context.Principal?.Identity?.IsAuthenticated == true).ToString(),
                           ["ClaimsCount"] = context.Principal?.Claims.Count().ToString() ?? "0",
                           ["RequestPath"] = context.HttpContext.Request.Path,
                       }
                   });
                   return Task.CompletedTask;
               };

               options.Events.OnRedirectToLogin = context =>
               {
                   var store = context.HttpContext.RequestServices.GetRequiredService<OidcEventStore>();
                    store.Add(new OidcEvent
                    {
                         EventType = "Cookie: OnRedirectToLogin (Challenge)",
                         Description = "Unauthenticated user hit a protected resource. The middleware redirects to the sign-in page to start authentication.",
                         Category = EventCategory.Infrastructure,
                         FlowPhase = "Challenge",
                       RequestPath = context.HttpContext.Request.Path,
                       Details = new Dictionary<string, string>
                       {
                           ["RedirectUrl"] = context.RedirectUri,
                           ["OriginalPath"] = context.HttpContext.Request.Path,
                           ["Reason"] = "user not authenticated",
                       }
                   });
                   return Task.CompletedTask;
               };

               options.Events.OnRedirectToAccessDenied = context =>
               {
                   var store = context.HttpContext.RequestServices.GetRequiredService<OidcEventStore>();
                    store.Add(new OidcEvent
                    {
                         EventType = "Cookie: OnRedirectToAccessDenied (Forbidden)",
                         Description = "Authenticated user lacks the required role or permission for the requested resource. The middleware redirects to the access-denied page.",
                         Category = EventCategory.Infrastructure,
                         FlowPhase = "Challenge",
                       RequestPath = context.HttpContext.Request.Path,
                       Details = new Dictionary<string, string>
                       {
                           ["RedirectUrl"] = context.RedirectUri,
                           ["OriginalPath"] = context.HttpContext.Request.Path,
                           ["Reason"] = "insufficient permissions",
                       }
                   });
                   return Task.CompletedTask;
               };
           });

    builder.Services.AddAuthorization();
}
else
{
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
           .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("EntraExternalId"))
           .EnableTokenAcquisitionToCallDownstreamApi()
           .AddInMemoryTokenCaches();

    builder.Services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
           .Configure<IServiceProvider>((oidc, serviceProvider) =>
           {
               var store = serviceProvider.GetRequiredService<OidcEventStore>();

               oidc.Events.OnRedirectToIdentityProvider = context =>
               {
                   var request = context.ProtocolMessage;
                    store.Add(new OidcEvent
                    {
                        EventType = "OnRedirectToIdentityProvider",
                        Description = "The app redirects your browser to the IdP's /authorize endpoint with client_id, redirect_uri, scope, state, and nonce parameters.",
                        FlowPhase = "SignIn",
                       Details = new Dictionary<string, string>
                       {
                           ["Authority"] = request.IssuerAddress ?? "unknown",
                           ["ClientId"] = request.ClientId ?? "unknown",
                           ["RedirectUri"] = request.RedirectUri ?? "unknown",
                           ["Scope"] = request.Scope ?? "unknown",
                           ["ResponseType"] = request.ResponseType ?? "unknown",
                           ["State (hash)"] = request.State is not null ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(request.State)))[..16] : "none",
                           ["Nonce (hash)"] = request.Nonce is not null ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(request.Nonce)))[..16] : "none",
                           ["Prompt"] = request.Prompt ?? "none",
                       }
                   });
                   return Task.CompletedTask;
               };

               oidc.Events.OnAuthorizationCodeReceived = async context =>
               {
                   var code = context.ProtocolMessage?.Code ?? context.TokenEndpointRequest?.Code;
                    store.Add(new OidcEvent
                    {
                        EventType = "OnAuthorizationCodeReceived",
                        Description = "The IdP redirected back with an authorization code. The middleware validates that state and nonce match before exchanging this code for tokens.",
                        FlowPhase = "SignIn",
                       Details = new Dictionary<string, string>
                       {
                           ["AuthorizationCode (masked)"] = code is not null ? $"{code[..Math.Min(8, code.Length)]}...[{code.Length} chars]" : "none",
                           ["RedirectUri"] = context.Properties?.RedirectUri ?? "unknown",
                           ["State validated"] = "yes (automatic by middleware)",
                       }
                   });

                    try
                    {
                        store.Add(new OidcEvent
                        {
                            EventType = "Client Secret Sent to /token endpoint",
                            Description = "The app POSTs the authorization code + client_secret + redirect_uri to the IdP's /token endpoint. The client_secret proves the app's identity — this is the confidential client handshake.",
                            FlowPhase = "SignIn",
                            Details = new Dictionary<string, string>
                            {
                                ["TokenEndpoint"] = "https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token",
                                ["RequestBody (redacted)"] = "grant_type=authorization_code&code=***&redirect_uri=/signin-oidc&client_id=***&client_secret=***[SENT]",
                                ["ClientSecret"] = "*** (sent, never logged)",
                                ["ClientAuthMethod"] = "client_secret_post",
                            }
                        });

                        var tokenAcquisition = context.HttpContext.RequestServices.GetRequiredService<ITokenAcquisition>();
                        var accessToken = await tokenAcquisition.GetAccessTokenForUserAsync(
                            scopes: ["openid", "profile"],
                            tokenAcquisitionOptions: new TokenAcquisitionOptions { ForceRefresh = false });

                         store.Add(new OidcEvent
                         {
                             EventType = "TokenExchange (post-code)",
                             Description = "The app sends the authorization code to the IdP's /token endpoint in exchange for an ID token, access token, and refresh token.",
                             FlowPhase = "SignIn",
                           Details = new Dictionary<string, string>
                           {
                               ["AccessToken (masked)"] = accessToken is not null ? $"{accessToken[..Math.Min(20, accessToken.Length)]}...[{accessToken.Length} chars]" : "failed",
                               ["TokenType"] = "Bearer",
                               ["TokenSource"] = "InMemoryTokenCache",
                           }
                       });

                       var handler = new JwtSecurityTokenHandler();
                       if (handler.CanReadToken(accessToken))
                       {
                           var parsed = handler.ReadJwtToken(accessToken);
                            store.Add(new OidcEvent
                            {
                                EventType = "TokenExchange: Parsed Access Token",
                                Description = "The received access token was decoded on the server side to inspect its claims (issuer, audience, scopes) without sending it to the client.",
                                FlowPhase = "SignIn",
                               Details = new Dictionary<string, string>
                               {
                                   ["Issuer"] = parsed.Issuer,
                                   ["Audience"] = string.Join(", ", parsed.Audiences),
                                   ["Subject"] = parsed.Subject,
                                   ["ValidFrom"] = parsed.ValidFrom.ToString("O"),
                                   ["ValidTo"] = parsed.ValidTo.ToString("O"),
                                   ["Claims Count"] = parsed.Claims.Count().ToString(),
                                   ["Scopes"] = parsed.Claims.FirstOrDefault(c => c.Type == "scp")?.Value ?? parsed.Claims.FirstOrDefault(c => c.Type == "http://schemas.microsoft.com/identity/claims/scope")?.Value ?? "none",
                               }
                           });
                       }
                   }
                   catch (Exception ex)
                   {
                        store.Add(new OidcEvent
                        {
                            EventType = "TokenExchange (error)",
                            Description = "The token exchange with the IdP failed. No tokens were issued — check the error details.",
                            FlowPhase = "SignIn",
                           Details = new Dictionary<string, string>
                           {
                               ["ErrorType"] = ex.GetType().Name,
                               ["ErrorMessage"] = ex.Message,
                           }
                       });
                   }
               };

               oidc.Events.OnTokenValidated = context =>
               {
                   var token = context.SecurityToken as JwtSecurityToken;
                   var principal = context.Principal;

                    store.Add(new OidcEvent
                    {
                        EventType = "OnTokenValidated (ID Token)",
                        Description = "The ID token's signature, issuer, audience, and lifetime passed validation. Your identity (ClaimsPrincipal) is now built from the token's claims.",
                        FlowPhase = "SignIn",
                       Details = new Dictionary<string, string>
                       {
                           ["Issuer"] = token?.Issuer ?? "unknown",
                           ["Audience"] = token?.Audiences is not null ? string.Join(", ", token.Audiences) : "unknown",
                           ["Subject"] = token?.Subject ?? "unknown",
                           ["ValidFrom"] = token?.ValidFrom.ToString("O") ?? "unknown",
                           ["ValidTo"] = token?.ValidTo.ToString("O") ?? "unknown",
                           ["Signature Valid"] = token is not null ? "yes" : "n/a",
                           ["SecurityToken Valid"] = "yes (middleware validated)",
                           ["AuthenticationType"] = principal?.Identity?.AuthenticationType ?? "unknown",
                           ["Name"] = principal?.Identity?.Name ?? "unknown",
                           ["TenantId"] = principal?.FindFirstValue("tid") ?? principal?.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid") ?? "unknown",
                           ["ObjectId"] = principal?.FindFirstValue("oid") ?? principal?.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier") ?? "unknown",
                           ["Claims Count"] = principal?.Claims.Count().ToString() ?? "0",
                       }
                   });
                   return Task.CompletedTask;
               };

               oidc.Events.OnAuthenticationFailed = context =>
               {
                    store.Add(new OidcEvent
                    {
                        EventType = "OnAuthenticationFailed",
                        Description = "OIDC authentication failed — the token was invalid, expired, or the signature didn't match the expected issuer.",
                        FlowPhase = "Failure",
                       Details = new Dictionary<string, string>
                       {
                           ["ExceptionType"] = context.Exception?.GetType().FullName ?? "unknown",
                           ["ExceptionMessage"] = context.Exception?.Message ?? "unknown",
                           ["RequestPath"] = context.HttpContext.Request.Path,
                       }
                   });
                   return Task.CompletedTask;
               };

               oidc.Events.OnRedirectToIdentityProviderForSignOut = context =>
               {
                   var request = context.ProtocolMessage;
                    store.Add(new OidcEvent
                    {
                        EventType = "OnRedirectToIdentityProviderForSignOut",
                        Description = "The app redirects your browser to the IdP's /logout endpoint to end the session at the identity provider.",
                        FlowPhase = "SignOut",
                       Details = new Dictionary<string, string>
                       {
                           ["LogoutUri"] = request?.IssuerAddress ?? "unknown",
                           ["PostLogoutRedirectUri"] = request?.PostLogoutRedirectUri ?? "unknown",
                           ["SessionId"] = context.Properties?.GetString("sid") ?? "not sent",
                       }
                   });
                   return Task.CompletedTask;
               };

               oidc.Events.OnRemoteSignOut = context =>
               {
                    store.Add(new OidcEvent
                    {
                        EventType = "OnRemoteSignOut",
                        Description = "The IdP confirmed the remote session was ended. The app now clears its local session cookie.",
                        FlowPhase = "SignOut",
                       Details = new Dictionary<string, string>
                       {
                           ["SessionId (from properties)"] = context.Properties?.GetString("sid") ?? "unknown",
                           ["ProtocolMessage"] = context.ProtocolMessage?.ToString() ?? "unknown",
                       }
                   });
                   return Task.CompletedTask;
               };

               oidc.Events.OnRemoteFailure = context =>
               {
                    store.Add(new OidcEvent
                    {
                        EventType = "OnRemoteFailure",
                        Description = "The remote sign-out or authentication failed at the IdP side — an error or user cancellation occurred.",
                        FlowPhase = "Failure",
                       Details = new Dictionary<string, string>
                       {
                           ["FailureType"] = context.Failure?.GetType().FullName ?? "unknown",
                           ["ErrorMessage"] = context.Failure?.Message ?? "unknown",
                           ["RequestPath"] = context.HttpContext.Request.Path,
                       }
                   });
                   return Task.CompletedTask;
               };
           });

    builder.Services.AddAuthentication()
           .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwt =>
           {
                var azureAd = builder.Configuration.GetSection("EntraExternalId");
               var tenantId = azureAd["TenantId"] ?? "common";
               var clientId = azureAd["ClientId"] ?? "";
               jwt.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
               jwt.TokenValidationParameters = new TokenValidationParameters
               {
                   ValidateIssuer = true,
                   ValidAudiences = [clientId],
                   ValidateLifetime = true,
                   ValidateIssuerSigningKey = true,
                   RoleClaimType = "roles"
               };
               jwt.MapInboundClaims = false;
               jwt.Events = new JwtBearerEvents
               {
                   OnTokenValidated = context =>
                   {
                       var store = context.HttpContext.RequestServices.GetRequiredService<OidcEventStore>();
                        store.Add(new OidcEvent
                        {
                             EventType = "JwtBearer: OnTokenValidated",
                             Description = "The access token presented to this API was validated (signature, issuer, audience, expiry). The user is authenticated for this API call.",
                             Category = EventCategory.ApiAuth,
                             FlowPhase = "ApiCall",
                            RequestPath = context.HttpContext.Request.Path,
                           Details = new Dictionary<string, string>
                           {
                               ["Issuer"] = context.Principal?.FindFirstValue("iss") ?? "unknown",
                               ["Audience"] = context.Principal?.FindFirstValue("aud") ?? "unknown",
                               ["Subject"] = context.Principal?.FindFirstValue("sub") ?? "unknown",
                               ["Scopes"] = context.Principal?.FindFirstValue("scp") ?? context.Principal?.FindFirstValue("http://schemas.microsoft.com/identity/claims/scope") ?? "none",
                               ["Roles"] = context.Principal is not null ? string.Join(", ", context.Principal.FindAll("roles").Select(c => c.Value)) : "none",
                               ["AppId"] = context.Principal?.FindFirstValue("appid") ?? context.Principal?.FindFirstValue("azp") ?? "unknown",
                               ["Claims Count"] = context.Principal?.Claims.Count().ToString() ?? "0",
                           }
                       });
                       return Task.CompletedTask;
                   },
                   OnAuthenticationFailed = context =>
                   {
                       var store = context.HttpContext.RequestServices.GetRequiredService<OidcEventStore>();
                        store.Add(new OidcEvent
                        {
                             EventType = "JwtBearer: OnAuthenticationFailed",
                             Description = "The access token failed validation — expired, malformed, or signed by an untrusted issuer. The API returns 401.",
                             Category = EventCategory.ApiAuth,
                             FlowPhase = "Failure",
                            RequestPath = context.HttpContext.Request.Path,
                           Details = new Dictionary<string, string>
                           {
                               ["ExceptionType"] = context.Exception?.GetType().FullName ?? "unknown",
                               ["ExceptionMessage"] = context.Exception?.Message ?? "unknown",
                           }
                       });
                       return Task.CompletedTask;
                   },
                   OnChallenge = context =>
                   {
                       var store = context.HttpContext.RequestServices.GetRequiredService<OidcEventStore>();
                        store.Add(new OidcEvent
                        {
                             EventType = "JwtBearer: OnChallenge",
                             Description = "No valid access token was provided with the request. The API returns 401 to tell the client it needs to obtain a token.",
                             Category = EventCategory.ApiAuth,
                             FlowPhase = "ApiCall",
                            RequestPath = context.HttpContext.Request.Path,
                           Details = new Dictionary<string, string>
                           {
                               ["Error"] = context.Error ?? "none",
                               ["ErrorDescription"] = context.ErrorDescription ?? "none",
                               ["AuthenticateFailure"] = context.AuthenticateFailure?.Message ?? "none",
                               ["Reason"] = context.AuthenticateFailure is not null ? "token rejected" : "no token provided",
                           }
                       });
                       return Task.CompletedTask;
                   },
                   OnForbidden = context =>
                   {
                       var store = context.HttpContext.RequestServices.GetRequiredService<OidcEventStore>();
                        store.Add(new OidcEvent
                        {
                             EventType = "JwtBearer: OnForbidden",
                             Description = "The token is valid but the user lacks the required scope or role for this endpoint. The API returns 403 Forbidden.",
                             Category = EventCategory.ApiAuth,
                             FlowPhase = "ApiCall",
                            RequestPath = context.HttpContext.Request.Path,
                           Details = new Dictionary<string, string>
                           {
                               ["User"] = context.HttpContext.User.Identity?.Name ?? "anonymous",
                               ["RequiredPolicy"] = "role or scope check failed",
                           }
                       });
                       return Task.CompletedTask;
                   }
               };
           });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireUserImpersonation", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context =>
            {
                var scopes = context.User.FindAll("scp")
                    .Concat(context.User.FindAll("http://schemas.microsoft.com/identity/claims/scope"))
                    .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                return scopes.Any(s => s.Equals("User.Impersonation", StringComparison.Ordinal) ||
                                       s.Equals("user_impersonation", StringComparison.Ordinal));
            });
        });
    });
}

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseExceptionHandler("/error");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.Use(async (HttpContext httpContext, Func<Task> next) =>
{
    if (httpContext.Request.Path.StartsWithSegments("/api"))
    {
        var store = httpContext.RequestServices.GetRequiredService<OidcEventStore>();
        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated == true;
        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();

        store.Add(new OidcEvent
        {
            EventType = isAuthenticated ? "API Request: Authenticated" : "API Request: Unauthenticated",
            Description = isAuthenticated ? "An API endpoint was called with a valid authentication cookie/token. The cookie is passed automatically by the browser on every request." : "An API endpoint was called without authentication — no cookie or token was present.",
            Category = EventCategory.ApiAuth,
            FlowPhase = "ApiCall",
            RequestPath = httpContext.Request.Path,
            Details = new Dictionary<string, string>
            {
                ["Method"] = httpContext.Request.Method,
                ["Path"] = httpContext.Request.Path,
                ["Authenticated"] = isAuthenticated.ToString(),
                ["AuthScheme"] = httpContext.User.Identity?.AuthenticationType ?? "none",
                ["AuthHeaderPresent"] = (authHeader is not null).ToString(),
                ["AuthHeaderType"] = authHeader?.Split(' ').FirstOrDefault() ?? "none",
                ["CookieCount"] = httpContext.Request.Headers.Cookie.Count.ToString(),
                ["Name"] = httpContext.User.Identity?.Name ?? "anonymous",
                ["Scopes"] = httpContext.User.FindFirst("scp")?.Value ?? "none",
                ["Roles"] = string.Join(", ", httpContext.User.FindAll("roles").Select(c => c.Value)),
            }
        });
    }
    await next();
});

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

if (authMode == "Testing")
{
    app.MapGet("/testing/sign-in/{role}", async (HttpContext httpContext, string role) =>
    {
        var store = httpContext.RequestServices.GetRequiredService<OidcEventStore>();

        var roles = role.ToLowerInvariant() switch
        {
            "customer" => Array.Empty<string>(),
            "operator" => ["Operator"],
            "admin" => ["Administrator"],
            "admin-operator" => ["Administrator", "Operator"],
            _ => ["Customer"]
        };

        store.Add(new OidcEvent
        {
             EventType = "Testing: Challenge (Redirect to IdP)",
            Description = "[Simulated] What the OIDC redirect to the IdP's /authorize endpoint looks like — shows the parameters that would be sent.",
            Category = EventCategory.Protocol,
            FlowPhase = "Challenge",
            Details = new Dictionary<string, string>
            {
                ["Authority"] = "https://login.microsoftonline.com/demo-testing/v2.0 (simulated)",
                ["ClientId"] = "demo-app",
                ["RedirectUri"] = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/signin-oidc",
                ["ResponseType"] = "code",
                ["Scope"] = "openid profile email",
                ["Prompt"] = "select_account",
            }
        });

        store.Add(new OidcEvent
        {
             EventType = "Testing: Authorization Code Received",
            Description = "[Simulated] The IdP redirected back with an authorization code in the query string, which would normally be exchanged for tokens.",
            Category = EventCategory.Protocol,
            FlowPhase = "SignIn",
            Details = new Dictionary<string, string>
            {
                ["Code"] = "simulated_code_abc123...",
                ["State"] = "validated",
            }
        });

        store.Add(new OidcEvent
        {
             EventType = "Testing: Client Secret Sent to /token endpoint",
            Description = "[Simulated] The client_secret is sent alongside the authorization code in the POST body to the /token endpoint. This authenticates the app (confidential client).",
            Category = EventCategory.Protocol,
            FlowPhase = "SignIn",
            Details = new Dictionary<string, string>
            {
                ["TokenEndpoint"] = "https://login.microsoftonline.com/demo-testing/v2.0/token (simulated)",
                ["RequestBody (redacted)"] = "grant_type=authorization_code&code=***&redirect_uri=/signin-oidc&client_id=demo-app&client_secret=***[SENT]",
                ["ClientSecret"] = "*** (simulated, never logged)",
            }
        });

        store.Add(new OidcEvent
        {
             EventType = "Testing: Token Exchange (code→tokens)",
            Description = "[Simulated] The authorization code is sent to the IdP's /token endpoint in exchange for an ID token and access token.",
            Category = EventCategory.Protocol,
            FlowPhase = "SignIn",
            Details = new Dictionary<string, string>
            {
                ["TokenType"] = "Bearer",
                ["AccessToken"] = "simulated...",
                ["IDToken"] = "simulated...",
                ["Source"] = "Testing mode bypass",
            }
        });

        var claims = new List<Claim>
        {
            new("tid", "11111111-1111-1111-1111-111111111111"),
            new("oid", Guid.NewGuid().ToString("D")),
            new("sub", Guid.NewGuid().ToString("D")),
            new("name", $"Demo {role}"),
            new("preferred_username", $"{role}@demo.example.com"),
            new("scp", "openid profile User.Impersonation"),
            new("iss", "https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111/v2.0"),
            new("aud", "demo-app"),
        };

        claims.AddRange(roles.Select(r => new Claim("roles", r)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        store.Add(new OidcEvent
        {
             EventType = "Testing: ID Token Validated",
            Description = "[Simulated] The ID token's claims are extracted and validated. This builds the user's identity that will be stored in the cookie.",
            Category = EventCategory.Protocol,
            FlowPhase = "SignIn",
            Details = new Dictionary<string, string>
            {
                ["Issuer"] = "https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111/v2.0",
                ["Audience"] = "demo-app",
                ["Subject"] = principal.FindFirstValue("sub") ?? "unknown",
                ["Name"] = principal.Identity?.Name ?? "unknown",
                ["TenantId"] = principal.FindFirstValue("tid") ?? "unknown",
                ["ObjectId"] = principal.FindFirstValue("oid") ?? "unknown",
                ["Claims Count"] = principal.Claims.Count().ToString(),
                ["Scopes"] = principal.FindFirstValue("scp") ?? "none",
                ["Roles"] = string.Join(", ", roles),
            }
        });

        store.Add(new OidcEvent
        {
             EventType = "Testing: Cookie Created",
            Description = "[Simulated] The session cookie is written to the browser, storing the user's claims so they persist across requests without re-authenticating.",
            Category = EventCategory.Infrastructure,
            FlowPhase = "SignIn",
            Details = new Dictionary<string, string>
            {
                ["CookieName"] = ".AspNetCore.Cookies",
                ["HttpOnly"] = "true",
                ["SameSite"] = "Lax",
                ["Secure"] = "false",
                ["Persistent"] = "true",
                ["Expires"] = "1 hour",
                ["Claims Stored"] = principal.Claims.Count().ToString(),
                ["User"] = principal.Identity?.Name ?? "unknown",
            }
        });

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Results.Redirect("/");
    }).AllowAnonymous();

    app.MapGet("/testing/sign-out", async (HttpContext httpContext) =>
    {
        var store = httpContext.RequestServices.GetRequiredService<OidcEventStore>();
        store.Add(new OidcEvent
        {
             EventType = "Testing: Sign-Out Challenge (Redirect to IdP)",
            Description = "[Simulated] The sign-out redirect to the IdP's /logout endpoint to end the session at the identity provider.",
            Category = EventCategory.Protocol,
            FlowPhase = "SignOut",
            Details = new Dictionary<string, string>
            {
                ["LogoutUri"] = "https://login.microsoftonline.com/demo-testing/v2.0/logout (simulated)",
                ["PostLogoutRedirectUri"] = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/",
            }
        });

        store.Add(new OidcEvent
        {
             EventType = "Testing: Local Cookie Cleared",
            Description = "[Simulated] The local session cookie is deleted, ending the local session. The remote IdP session would also be ended in a real flow.",
            Category = EventCategory.Infrastructure,
            FlowPhase = "SignOut",
            Details = new Dictionary<string, string>
            {
                ["CookieName"] = ".AspNetCore.Cookies",
                ["Action"] = "deleted",
                ["RemoteSignOut"] = "bypassed (Testing mode)",
            }
        });

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Results.Redirect("/");
    }).AllowAnonymous();
}

app.Run();
