using Adhicom.Application.Administration;
using Adhicom.Application.Catalog;
using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Orders;
using Adhicom.Application.Payments;
using Adhicom.Application.Stats;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Domain.Common;
using Adhicom.Infrastructure.Identity;
using Adhicom.Infrastructure.Payments;
using Adhicom.Infrastructure.Persistence;
using Adhicom.Infrastructure.Seed;
using Adhicom.Infrastructure.Services;
using Adhicom.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ===================== Services =====================
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                       | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                       | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Base de données : PostgreSQL partout (l'environnement Testing réutilise la connexion
// fournie par la variable d'environnement/le secret — la CI fournit adhicom_test sur PostgreSQL).
var testEnvironment = builder.Environment.IsEnvironment("Testing");
builder.Services.AddDbContext<AdhicomDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection est requise.");
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3));
});

// Authentification : ASP.NET Core Identity (mots de passe hachés, verrouillage anti force brute)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AdhicomDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "adhicom.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/access-denied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

// Contexte par requête
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<IClock, Clock>();
builder.Services.AddSingleton<IOrderNumberGenerator, OrderNumberGenerator>();

// Services métier
builder.Services.AddScoped<IStoreService, EfStoreService>();
builder.Services.AddScoped<IProductService, EfProductService>();
builder.Services.AddScoped<ICategoryService, EfCategoryService>();
builder.Services.AddScoped<IInventoryService, EfInventoryService>();
builder.Services.AddScoped<ICartService, EfCartService>();
builder.Services.AddScoped<ICustomerService, EfCustomerService>();
builder.Services.AddScoped<IOrderService, EfOrderService>();
builder.Services.AddScoped<ISubscriptionLimitService, SubscriptionLimitService>();
builder.Services.AddScoped<ISubscriptionMaintenanceService, SubscriptionMaintenanceService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ISupportService, SupportService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<ISiteContentService, SiteContentService>();
builder.Services.AddScoped<IPaymentProviderRegistry, PaymentProviderRegistry>();
builder.Services.AddScoped<IPaymentWebhookProcessor, PaymentWebhookProcessor>();

builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
if (string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Host"]))
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

builder.Services.AddHostedService<SubscriptionMaintenanceHostedService>();

var app = builder.Build();

// ===================== Initialisation base de données =====================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
    try
    {
        var db = services.GetRequiredService<AdhicomDbContext>();
        if (testEnvironment)
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            var hasMigrations = db.Database.GetMigrations().Any();
            if (hasMigrations) await db.Database.MigrateAsync();
            else await db.Database.EnsureCreatedAsync(); // Lance `dotnet ef migrations add InitialCreate` pour générer les migrations
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var tenant = services.GetRequiredService<ITenantContext>();
        await DatabaseSeeder.SeedAsync(db, userManager, roleManager, app.Configuration, tenant, logger);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Échec de l'initialisation de la base de données. Vérifiez la chaîne de connexion et que PostgreSQL est démarré.");
        throw;
    }
}

// ===================== Pipeline =====================
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    // En Development, la page d'erreur développeur est fournie automatiquement par le hosting.
    app.UseExceptionHandler("/erreur");
    app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers["Cache-Control"] = ctx.Context.Request.Path.StartsWithSegments("/css") || ctx.Context.Request.Path.StartsWithSegments("/js") || ctx.Context.Request.Path.StartsWithSegments("/img")
            ? "public,max-age=604800"
            : "public,max-age=3600"
});

app.UseMiddleware<HostResolutionMiddleware>();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapControllers(); // routage par attributs (webhooks)

app.MapControllerRoute(
    name: "app-shipping",
    pattern: "app/settings/shipping/{action=Index}/{id?}",
    defaults: new { area = "App", controller = "Shipping" });

app.MapControllerRoute(
    name: "app-payments",
    pattern: "app/settings/payments/{action=Index}/{id?}",
    defaults: new { area = "App", controller = "PaymentMethods" });

app.MapControllerRoute(
    name: "merchant",
    pattern: "app/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { area = "App" });

app.MapControllerRoute(
    name: "admin",
    pattern: "admin/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { area = "Admin" });

app.MapControllerRoute(
    name: "storefront-product",
    pattern: "storefront/{storeSlug}/Product/{productSlug}",
    defaults: new { area = "Storefront", controller = "Storefront", action = "Product" });

app.MapControllerRoute(
    name: "storefront-confirmation",
    pattern: "storefront/{storeSlug}/Confirmation/{orderNumber}",
    defaults: new { area = "Storefront", controller = "Storefront", action = "Confirmation" });

app.MapControllerRoute(
    name: "storefront",
    pattern: "storefront/{storeSlug}/{action=Index}/{id?}",
    defaults: new { area = "Storefront", controller = "Storefront" });

// Pages publiques « propres » (URLs lisibles pour le SEO)
app.MapControllerRoute(name: "fonctionnalites", pattern: "fonctionnalites", defaults: new { controller = "Home", action = "Fonctionnalites" });
app.MapControllerRoute(name: "tarifs", pattern: "tarifs", defaults: new { controller = "Home", action = "Tarifs" });
app.MapControllerRoute(name: "faq", pattern: "faq", defaults: new { controller = "Home", action = "Faq" });
app.MapControllerRoute(name: "cgu", pattern: "cgu", defaults: new { controller = "Home", action = "Cgu" });
app.MapControllerRoute(name: "confidentialite", pattern: "confidentialite", defaults: new { controller = "Home", action = "Confidentialite" });
app.MapControllerRoute(name: "contact", pattern: "contact", defaults: new { controller = "Contact", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>Point d'entrée public pour les tests d'intégration (WebApplicationFactory).</summary>
public partial class Program { }
