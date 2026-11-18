using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ScheduleLogic.Server.Class;
using ScheduleLogic.Server.Models;
using ScheduleLogic.Server.Services;
using ScheduleLogic.Server.Services.Interfaces;
using ScheduleLogic.Server.Services.Security;
using System.Net;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Konfiguráció: appsettings.json -> appsettings.{Környezet}.json -> User Secrets
// (csak Development) -> környezeti változók (pl. Database__Password).
// Jelszó, kulcs, token NEM kerülhet az appsettings fájlokba, lásd KONFIGURACIO.md.

builder.Services.AddExceptionHandler<ExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllersWithViews();

// CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins("https://localhost:58921")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.WithOrigins("https://schedulelogic.hu")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never;
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

// --- Adatbázis ---------------------------------------------------------------
// A kapcsolati adatok a "Database" szekcióból jönnek; a jelszó User Secretsből
// (fejlesztés) vagy a Database__Password környezeti változóból (éles).
// A builder escape-eli az értékeket (pl. ';' a jelszóban nem töri el a stringet).
var database = builder.Configuration.GetSection("Database");
if (string.IsNullOrEmpty(database["Password"]))
{
    throw new InvalidOperationException(
        "Hiányzik az adatbázis jelszava (Database:Password). Fejlesztéskor: " +
        "dotnet user-secrets set \"Database:Password\" \"...\" --project ScheduleLogic.Server; " +
        "élesben a Database__Password környezeti változó. Részletek: KONFIGURACIO.md");
}
var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = database["Host"] ?? "localhost",
    Port = int.TryParse(database["Port"], out var dbPort) ? dbPort : 5432,
    Database = database["Name"] ?? "scheduleLogic",
    Username = database["User"],
    Password = database["Password"],
}.ConnectionString;

builder.Services.AddDbContext<ScheduleLogicContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();

builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IEventRepository, EventRepository>();

builder.Services.AddHttpClient("Solver", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["URLS:Solver"]!
    );
});
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// --- Biztonság ---------------------------------------------------------------
builder.Services.AddSingleton<SolverCallbackSigner>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ServerSideTicketStore>();
builder.Services.AddScheduleLogicRateLimiting(builder.Configuration);

// Reverse proxy (nginx) mögött a valódi kliens IP-je és sémája az
// X-Forwarded-For / X-Forwarded-Proto fejlécből jön, de csak a megbízható
// proxytól (ForwardedHeaders:KnownProxies, alapból a localhost).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();
    var proxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>()
                  ?? new[] { "127.0.0.1", "::1" };
    foreach (var proxy in proxies)
        if (IPAddress.TryParse(proxy, out var ip)) options.KnownProxies.Add(ip);
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/api/Authentication/login";
    options.LogoutPath = "/api/Authentication/logout";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.SlidingExpiration = true;
    options.Cookie.Name = "sl_auth";

    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };

    // SameSite=Lax mindenhol: a frontend ugyanarról a domainről fut, így a
    // korábbi SameSite=None csak azt tette lehetővé, hogy egy idegen oldal a
    // felhasználó nevében küldjön kéréseket (CSRF). Domain sincs megadva, így
    // a süti csak a pontos hosztra megy (aldomainekre nem).
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
})
.AddCookie("AdminCookie", options =>
{
    options.Cookie.Name = "sl_admin_auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.LoginPath = "/admin/login";
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;

    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/admin/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// Mindkét séma szerveroldali munkamenetet használ: kijelentkezéskor a régi
// süti azonnal érvénytelen lesz (lásd ServerSideTicketStore).
foreach (var scheme in new[] { CookieAuthenticationDefaults.AuthenticationScheme, "AdminCookie" })
{
    builder.Services.AddOptions<CookieAuthenticationOptions>(scheme)
        .Configure<ServerSideTicketStore>((options, store) => options.SessionStore = store);
}

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    // A böngésző ne találgassa a tartalomtípust, és az oldal ne legyen
    // beágyazható más oldalba (clickjacking).
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    await next();
});

app.UseCors("AllowAngularApp");

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
