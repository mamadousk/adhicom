using Adhicom.Application.Common;
using Adhicom.Domain.Administration;
using Adhicom.Domain.Catalog;
using Adhicom.Domain.Common;
using Adhicom.Domain.Notifications;
using Adhicom.Domain.Payments;
using Adhicom.Domain.Shipping;
using Adhicom.Domain.Stores;
using Adhicom.Domain.Subscriptions;
using Adhicom.Infrastructure.Identity;
using Adhicom.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Adhicom.Infrastructure.Seed;

/// <summary>
/// Amorçage idempotent de la base : rôles, plans, prestataires, administrateur, paramètres
/// et (optionnellement, Seeds:DemoData=true) un jeu de démonstration Adhicom + Fashion House.
/// N'écrase jamais des données existantes.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(AdhicomDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
        IConfiguration config, ITenantContext tenant, ILogger logger, CancellationToken ct = default)
    {
        using (tenant.BypassScope())
        {
            await SeedRolesAsync(roleManager, ct);
            await SeedPlansAsync(db, ct);
            await SeedPaymentProvidersAsync(db, ct);
            await SeedSystemSettingsAsync(db, ct);
            await SeedNotificationTemplatesAsync(db, ct);
            await SeedSiteContentsAsync(db, ct);

            var adminEmail = config["AdminSeed:Email"] ?? "admin@adhicom.com";
            var adminPassword = config["AdminSeed:Password"] ?? "Admin123!";
            await SeedUserAsync(userManager, logger, adminEmail, adminPassword, "Admin", "Adhicom", AppRoles.SuperAdmin, ct);

            if (string.Equals(config["Seeds:DemoData"], "true", StringComparison.OrdinalIgnoreCase))
                await SeedDemoDataAsync(db, userManager, logger, ct);
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, CancellationToken ct)
    {
        foreach (var role in new[] { AppRoles.SuperAdmin, AppRoles.Merchant })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
    }

    private static async Task<ApplicationUser?> SeedUserAsync(UserManager<ApplicationUser> userManager, ILogger logger,
        string email, string password, string firstName, string lastName, string role, CancellationToken ct)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null) return existing;

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FirstName = firstName, LastName = lastName, IsActive = true };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogError("Impossible de créer l'utilisateur {Email} : {Errors}", email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return null;
        }
        await userManager.AddToRoleAsync(user, role);
        logger.LogInformation("Utilisateur {Role} créé : {Email}", role, email);
        return user;
    }

    private static async Task SeedPlansAsync(AdhicomDbContext db, CancellationToken ct)
    {
        if (await db.Plans.AnyAsync(ct)) return;

        var plans = new List<Plan>
        {
            new() { Key = "free", Name = "Gratuit", Description = "Pour démarrer et tester Adhicom sans risque.", MonthlyPrice = 0m, SortOrder = 1,
                    MaxStores = 1, MaxProducts = 20, MaxUsers = 1, MaxStorageMb = 100,
                    Features = new List<PlanFeature> { F("1 boutique", 1), F("20 produits", 2), F("Sous-domaine adhicom.com", 3), F("Paiement à la livraison et manuel", 4), F("Statistiques de base", 5) } },
            new() { Key = "starter", Name = "Starter", Description = "Pour les commerçants qui veulent vendre sérieusement en ligne.", MonthlyPrice = 9.99m, SortOrder = 2,
                    MaxStores = 1, MaxProducts = 100, MaxUsers = 1, MaxStorageMb = 2048, AllowExports = true,
                    Features = new List<PlanFeature> { F("1 boutique", 1), F("100 produits", 2), F("1 Go d'espace images", 3), F("Export CSV des clients", 4), F("Support prioritaire", 5) } },
            new() { Key = "pro", Name = "Pro", Description = "Pour les boutiques en croissance : plus de produits, plusieurs boutiques.", MonthlyPrice = 24.99m, SortOrder = 3,
                    MaxStores = 3, MaxProducts = 1000, MaxUsers = 3, MaxStorageMb = 10240, AllowPremiumThemes = true, AllowAdvancedAnalytics = true, AllowMarketing = true, AllowCoupons = true, AllowExports = true,
                    Features = new List<PlanFeature> { F("3 boutiques", 1), F("1 000 produits", 2), F("Thèmes premium", 3), F("Statistiques avancées", 4), F("Promotions et codes (bientôt)", 5), F("10 Go d'espace", 6) } },
            new() { Key = "business", Name = "Business", Description = "Pour les entreprises et distributeurs multi-boutiques.", MonthlyPrice = 49.99m, SortOrder = 4,
                    MaxStores = 10, MaxProducts = 100000, MaxUsers = 10, MaxStorageMb = 51200, AllowPremiumThemes = true, AllowAdvancedAnalytics = true, AllowMarketing = true, AllowCoupons = true, AllowExports = true, AllowCustomDomain = true,
                    Features = new List<PlanFeature> { F("10 boutiques", 1), F("Produits illimités", 2), F("Domaine personnalisé (bientôt)", 3), F("Analytique avancée", 4), F("50 Go d'espace", 5), F("ACCOMPAGNEMENT dédié", 6) } },
        };
        db.Plans.AddRange(plans);
        await db.SaveChangesAsync(ct);

        static PlanFeature F(string label, int order) => new() { Label = label, SortOrder = order };
    }

    private static async Task SeedPaymentProvidersAsync(AdhicomDbContext db, CancellationToken ct)
    {
        if (await db.PaymentProviders.AnyAsync(ct)) return;
        db.PaymentProviders.AddRange(
            new PaymentProvider { Key = PaymentMethods.CashOnDelivery, Name = "Paiement à la livraison", IsActive = true, SupportsWebhook = false },
            new PaymentProvider { Key = PaymentMethods.Manual, Name = "Paiement manuel (Mobile Money / virement)", IsActive = true, SupportsWebhook = false },
            new PaymentProvider { Key = PaymentMethods.BankTransfer, Name = "Virement bancaire", IsActive = true, SupportsWebhook = false },
            new PaymentProvider { Key = "moko", Name = "Moko Afrika", IsActive = false, SupportsWebhook = true, DocsUrl = "https://moko.africa" },
            new PaymentProvider { Key = "nomba", Name = "Nomba", IsActive = false, SupportsWebhook = true, SupportsRefund = true },
            new PaymentProvider { Key = "optgateway", Name = "OPTGateway (Orange Money)", IsActive = false, SupportsWebhook = true },
            new PaymentProvider { Key = "malipo", Name = "Malipo", IsActive = false, SupportsWebhook = true });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedSystemSettingsAsync(AdhicomDbContext db, CancellationToken ct)
    {
        var defaults = new Dictionary<string, (string Value, string Description)>
        {
            ["subscription.grace_days"] = ("7", "Durée (jours) de la période de grâce avant limitation des fonctionnalités."),
            ["platform.announcement"] = ("", "Message d'annonce global affiché dans les tableaux de bord (laisser vide pour masquer)."),
            ["contact.support_email"] = ("support@adhicom.com", "Adresse de contact du support."),
        };
        foreach (var (key, (value, description)) in defaults)
            if (!await db.SystemSettings.AnyAsync(s => s.Key == key, ct))
                db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, Description = description });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedNotificationTemplatesAsync(AdhicomDbContext db, CancellationToken ct)
    {
        if (await db.NotificationTemplates.AnyAsync(ct)) return;
        db.NotificationTemplates.AddRange(
            new NotificationTemplate { Key = "order.created", Channel = "inapp", Subject = "Nouvelle commande", Body = "La commande {{orderNumber}} est en attente de traitement." },
            new NotificationTemplate { Key = "order.status", Channel = "email", Subject = "Mise à jour de la commande {{orderNumber}}", Body = "Bonjour {{customerName}}, votre commande {{orderNumber}} est passée au statut : {{status}}." },
            new NotificationTemplate { Key = "subscription.expiring", Channel = "email", Subject = "Votre abonnement arrive à échéance", Body = "Bonjour, votre abonnement {{planName}} expire le {{endDate}}. Renouvelez-le depuis votre tableau de bord." },
            new NotificationTemplate { Key = "stock.low", Channel = "inapp", Subject = "Stock faible", Body = "Le produit {{productName}} est sous le seuil d'alerte ({{stock}} restants)." });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedSiteContentsAsync(AdhicomDbContext db, CancellationToken ct)
    {
        var defaults = new Dictionary<string, string>
        {
            ["home.hero_title"] = "Votre boutique en ligne, prête en quelques minutes.",
            ["home.hero_subtitle"] = "Créez, personnalisez et gérez votre boutique Adhicom : produits, commandes, clients et paiement à la livraison. Sans compétences techniques, adapté à la RDC et à l'Afrique francophone.",
            ["home.hero_cta"] = "Créer ma boutique gratuitement",
            ["home.announcement"] = "Lancement en République Démocratique du Congo — paiement à la livraison et Mobile Money bientôt automatisé.",
        };
        foreach (var (key, value) in defaults)
            if (!await db.SiteContents.AnyAsync(c => c.Key == key, ct))
                db.SiteContents.Add(new SiteContent { Key = key, Value = value, Description = "Contenu éditorial du site principal" });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedDemoDataAsync(AdhicomDbContext db, UserManager<ApplicationUser> userManager, ILogger logger, CancellationToken ct)
    {
        var demo = await SeedUserAsync(userManager, logger, "demo@adhicom.com", "Demo123!", "Marie", "Demo", AppRoles.Merchant, ct);
        if (demo?.Id is null) return;
        var ownerId = Guid.Parse(demo.Id);

        if (await db.Stores.AnyAsync(s => s.Slug == "fashionhouse", ct)) return;

        var store = new Store
        {
            Name = "Fashion House",
            Slug = "fashionhouse",
            Description = "Mode africaine contemporaine : prêt-à-porter, wax, accessoires — livré à Kinshasa et partout en RDC.",
            BusinessCategory = "Mode et vêtements",
            Country = "CD",
            City = "Kinshasa",
            Address = "Avenue de la Justice, Gombe",
            WhatsappNumber = "243990000000",
            BusinessEmail = "contact@fashionhouse.cd",
            OwnerId = ownerId,
            Status = StoreStatus.Active,
            ThemeKey = "mode",
            PrimaryColor = "#BE185D",
            SecondaryColor = "#F59E0B",
        };
        db.Stores.Add(store);
        db.StoreSettings.Add(new StoreSettings { StoreId = store.Id, Currency = Currencies.Usd, Timezone = "Africa/Kinshasa", DeliveryInfo = "Livraison à Kinshasa en 24–48h. Retrait disponible en boutique.", SeoTitle = "Fashion House — Mode africaine à Kinshasa", SeoDescription = "Découvrez la collection Fashion House : wax, prêt-à-porter et accessoires livrés à Kinshasa." });
        db.StoreDomains.Add(new StoreDomain { StoreId = store.Id, Domain = "fashionhouse.adhicom.com", IsPrimary = true, IsVerified = true, VerifiedAtUtc = DateTime.UtcNow });
        db.StoreMembers.Add(new StoreMember { StoreId = store.Id, UserId = ownerId, Role = "owner" });

        var mode = new Category { StoreId = store.Id, Name = "Mode femme", Slug = "mode-femme", DisplayOrder = 1, IsActive = true };
        var homme = new Category { StoreId = store.Id, Name = "Mode homme", Slug = "mode-homme", DisplayOrder = 2, IsActive = true };
        var accessoires = new Category { StoreId = store.Id, Name = "Accessoires", Slug = "accessoires", DisplayOrder = 3, IsActive = true };
        db.Categories.AddRange(mode, homme, accessoires);

        var products = new[]
        {
            ("Robe wax Élégance", 45m, (decimal?)59m, 24, mode, "robe-wax-elegance", "1"),
            ("Jupe crayon Ankara", 32m, null, 30, mode, "jupe-crayon-ankara", "2"),
            ("Ensemble pagne Royal", 68m, (decimal?)85m, 15, mode, "ensemble-pagne-royal", "3"),
            ("Chemise lin Kinshasa", 28m, null, 40, homme, "chemise-lin-kinshasa", "4"),
            ("Costume trois pièces Savane", 180m, null, 8, homme, "costume-savane", "5"),
            ("Baskets Urban Prestige", 75m, (decimal?)90m, 22, homme, "baskets-urban-prestige", "6"),
            ("Sac à main cuir artisanal", 55m, null, 18, accessoires, "sac-cuir-artisanal", "7"),
            ("Boucles d'oreilles Kongo", 18m, null, 60, accessoires, "boucles-kongo", "8"),
        };
        foreach (var (name, price, compareAt, stock, category, slugName, img) in products)
        {
            var product = new Product
            {
                StoreId = store.Id,
                Name = name,
                Slug = slugName,
                Description = $"{name} — pièce phare de la collection Fashion House, fabriquée artisanalement avec soin.",
                Price = price,
                CompareAtPrice = compareAt,
                StockQuantity = stock,
                Status = ProductStatus.Active,
                CategoryId = category.Id,
                WeightKg = 0.5m
            };
            db.Products.Add(product);
            db.ProductImages.Add(new ProductImage { StoreId = store.Id, ProductId = product.Id, Url = $"/img/demo/p{img}.svg", IsPrimary = true, DisplayOrder = 1 });
        }

        db.StorePaymentConfigurations.AddRange(
            new StorePaymentConfiguration { StoreId = store.Id, Method = PaymentMethods.CashOnDelivery, IsEnabled = true },
            new StorePaymentConfiguration { StoreId = store.Id, Method = PaymentMethods.Manual, IsEnabled = true, PublicInstructions = "Mobile Money : +243 99 000 00 00 (Marie). Envoyez la preuve de paiement après commande." },
            new StorePaymentConfiguration { StoreId = store.Id, Method = PaymentMethods.BankTransfer, IsEnabled = false });

        db.ShippingMethods.AddRange(
            new ShippingMethod { StoreId = store.Id, Name = "Livraison Kinshasa", Type = ShippingMethodType.Flat, Price = 5m, EstimatedDelivery = "24–48h", DisplayOrder = 1 },
            new ShippingMethod { StoreId = store.Id, Name = "Livraison province (RDC)", Type = ShippingMethodType.Flat, Price = 12m, EstimatedDelivery = "2–4 jours", DisplayOrder = 2 },
            new ShippingMethod { StoreId = store.Id, Name = "Retrait sur place (Gombe)", Type = ShippingMethodType.Pickup, Price = 0m, EstimatedDelivery = "Immédiat", DisplayOrder = 3 });

        // Abonnement Pro actif pour le compte démo (rapports & statistiques avancées)
        var pro = await db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Key == "pro", ct);
        if (pro is not null && !await db.Subscriptions.AnyAsync(s => s.SubscriberUserId == ownerId, ct))
        {
            db.Subscriptions.Add(new Subscription { SubscriberUserId = ownerId, PlanId = pro.Id, Status = SubscriptionStatus.Active, StartsAtUtc = DateTime.UtcNow.AddDays(-40), EndsAtUtc = DateTime.UtcNow.AddDays(325) });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Données de démonstration créées : boutique « fashionhouse » (demo@adhicom.com / Demo123!)");
    }
}
