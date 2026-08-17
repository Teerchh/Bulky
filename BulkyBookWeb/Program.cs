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
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Ensure consistent currency & number formatting across environments.
// Without this, Linux/App Service falls back to the invariant culture and prices render as "¤99.00".
// Change to "en-NG" for ₦ or "en-ZA" for R if preferred.
var appCulture = new CultureInfo("en-NG");
CultureInfo.DefaultThreadCurrentCulture = appCulture;
CultureInfo.DefaultThreadCurrentUICulture = appCulture;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

//configure stripe (only set if a key is provided, so the app can still run without payments configured)
var stripeSecretKey = builder.Configuration.GetSection("Stripe:SecretKey").Get<string>();
if (!string.IsNullOrWhiteSpace(stripeSecretKey))
{
    StripeConfiguration.ApiKey = stripeSecretKey;
}

app.UseRouting();
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