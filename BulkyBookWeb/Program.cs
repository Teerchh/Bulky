using Bulky.DataAccess.Data;
using Bulky.DataAccess.DBInitializer;
using Bulky.DataAccess.Repository;
using Bulky.DataAccess.Repository.IRepository;
using Azure.Storage.Blobs;
using Bulky.Utility;
using BulkyBookWeb.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Request-based localization: the culture is picked per request (browser Accept-Language / ?culture= / cookie),
// so storefront prices convert from the base currency (USD) to the visitor's currency via CurrencyService.
// Fully dynamic: every specific (language-region) culture is supported, so any visitor's browser locale is
// honored and prices convert to their currency. There are no .resx resources, so UI text stays English while
// number/currency formatting follows the visitor's locale. en-NG remains the fallback default.
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var allSpecificCultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
        .Where(c => !string.IsNullOrEmpty(c.Name))
        .OrderBy(c => c.Name)
        .ToArray();
    options.DefaultRequestCulture = new RequestCulture("en-NG");
    options.SupportedCultures = allSpecificCultures;
    options.SupportedUICultures = allSpecificCultures;
    // default provider order is used: QueryString (?culture=) > Cookie > Accept-Language (browser)
});

//fallback for non-request contexts (threads/background work)
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-NG");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-NG");

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("DefaultConnection"),
    npg => npg.EnableRetryOnFailure()));

//injects values of stripe in appsettings.json into stripesettings properties
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

//injects storage settings (Azure Blob Storage) into StorageSettings
builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection("Storage"));

//health checks - expose /health for uptime monitoring + App Service health checks
builder.Services.AddHealthChecks();

//Application Insights telemetry (only enabled when a connection string is configured)
// - On Azure: set APPLICATIONINSIGHTS_CONNECTION_STRING (or enable via portal "Application Insights")
// - Locally: left unset, so telemetry stays disabled and startup never fails
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
    ?? builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

//add identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();


//add google oauth (loaded from config: appsettings.Development.json locally, App Settings on Azure)
var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}


builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = $"/Identity/Account/Login";
    options.LogoutPath = $"/Identity/Account/Logout";
    options.AccessDeniedPath = $"/Identity/Account/AccessDenied";
});

//persist Data Protection keys to Blob Storage so auth cookies survive app restarts
//(only when Blob Storage is configured; keys live in a separate private container)
var storageConnectionString = builder.Configuration["Storage:ConnectionString"];
if (!string.IsNullOrWhiteSpace(storageConnectionString))
{
    var keysContainer = new BlobContainerClient(storageConnectionString, "dataprotection");
    keysContainer.CreateIfNotExistsAsync().GetAwaiter().GetResult();
    builder.Services.AddDataProtection()
        .SetApplicationName("BulkyWeb")
        .PersistKeysToAzureBlobStorage(keysContainer.GetBlobClient("dataprotection-keys.xml"));
}

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(100);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<IDBInitializer, DBInitializer>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();

var app = builder.Build();

//security headers for every response (CSP + clickjacking + MIME sniffing + referrer policy)
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    headers.ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://cdn.datatables.net https://cdn.tiny.cloud; " +
        "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://cdn.datatables.net https://fonts.googleapis.com; " +
        "img-src 'self' data: https://placehold.co https://*.blob.core.windows.net https://sp.tinymce.com; " +
        "font-src 'self' https://cdn.jsdelivr.net https://fonts.gstatic.com; " +
        "connect-src 'self' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://cdn.datatables.net https://cdn.tiny.cloud; " +
        "frame-src 'self';";
    await next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // cache static assets (css/js/images/fonts) in the browser for 7 days
        ctx.Context.Response.Headers.CacheControl = "public, max-age=604800";
    }
});

//configure stripe (only set if a key is provided, so the app can still run without payments configured)
var stripeSecretKey = builder.Configuration.GetSection("Stripe:SecretKey").Get<string>();
if (!string.IsNullOrWhiteSpace(stripeSecretKey))
{
    StripeConfiguration.ApiKey = stripeSecretKey;
}

app.UseRouting();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
        name: "default",
        pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");

app.MapHealthChecks("/health");

//Run migrations + seed only in Development. In production, migrations are applied by the
//CI/CD pipeline, so a slow/failing database can never take the site down at startup.
if (app.Environment.IsDevelopment())
{
    SeedDatabase();
}

app.MapRazorPages();

app.Run();


void SeedDatabase()
{
    using var scope = app.Services.CreateScope();
    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDBInitializer>();
    dbInitializer.Initialize();
}