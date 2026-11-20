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

builder.Services.AddExceptionHandler<ExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllersWithViews();

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

var database = builder.Configuration.GetSection("Database");
if (string.IsNullOrEmpty(database["Password"]))
{
    throw new InvalidOperationException(
        "The database password (Database:Password) is missing. In development: " +
        "dotnet user-secrets set \"Database:Password\" \"...\" --project ScheduleLogic.Server; " +
        "in production the Database__Password environment variable. Details: CONFIGURATION.md");
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

builder.Services.AddSingleton<SolverCallbackSigner>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ServerSideTicketStore>();
builder.Services.AddScheduleLogicRateLimiting(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    // Only the configured reverse proxies are trusted for X-Forwarded-For (used by the per-IP rate limiter).
    options.KnownIPNetworks.Clear();
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

foreach (var scheme in new[] { CookieAuthenticationDefaults.AuthenticationScheme, "AdminCookie" })
{
    builder.Services.AddOptions<CookieAuthenticationOptions>(scheme)
        // Sessions live on the server, so logout really invalidates a stolen cookie.
        .Configure<ServerSideTicketStore>((options, store) => options.SessionStore = store);
}

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
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
