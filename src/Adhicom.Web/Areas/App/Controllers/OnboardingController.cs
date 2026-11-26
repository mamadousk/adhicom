using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.App.Controllers;

public class OnboardingController : MerchantControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;

    public OnboardingController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db,
        IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        : base(storeService, tenant, currentUser, notifications, limits)
        => _db = db;

    public async Task<IActionResult> Index()
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var storeId = CurrentStore.Id;

        var hasLogo = !string.IsNullOrWhiteSpace(CurrentStore.LogoUrl);
        var hasTheme = !string.IsNullOrWhiteSpace(CurrentStore.ThemeKey);
        var hasColors = CurrentStore.PrimaryColor != "#0F4C3A";
        var hasCategories = await _db.Categories.AnyAsync(c => c.StoreId == storeId);
        var hasProducts = await _db.Products.AnyAsync(p => p.StoreId == storeId);
        var hasPayments = await _db.StorePaymentConfigurations.AnyAsync(p => p.StoreId == storeId && p.IsEnabled);
        var hasShipping = await _db.ShippingMethods.AnyAsync(m => m.StoreId == storeId && m.IsActive);
        var isPublished = CurrentStore.Status == Domain.Common.StoreStatus.Active;

        var steps = new List<SetupStep>
        {
            new() { Title = "Ajouter votre logo", Description = "Personnalisez l'identité visuelle de votre boutique.", Done = hasLogo, Link = "/app/settings" },
            new() { Title = "Choisir un thème", Description = "Mode, sneakers, électronique, beauté, alimentation ou généraliste.", Done = hasTheme, Link = "/app/settings#apparence" },
            new() { Title = "Personnaliser les couleurs", Description = "Couleurs principale et secondaire selon votre marque.", Done = hasColors, Link = "/app/settings#apparence" },
            new() { Title = "Créer vos catégories", Description = "Organisez votre catalogue (Mode, Accessoires...).", Done = hasCategories, Link = "/app/categories" },
            new() { Title = "Ajouter vos premiers produits", Description = "Nom, prix, stock, photos.", Done = hasProducts, Link = "/app/products/create" },
            new() { Title = "Configurer les paiements", Description = "Paiement à la livraison, Mobile Money manuel...", Done = hasPayments, Link = "/app/settings/payments" },
            new() { Title = "Configurer les livraisons", Description = "Zones, tarifs, retrait sur place.", Done = hasShipping, Link = "/app/settings/shipping" },
            new() { Title = "Publier la boutique", Description = "Votre boutique est visible par vos clients.", Done = isPublished, Link = "/app/settings" },
        };

        return View(new OnboardingViewModel
        {
            Store = CurrentStore,
            Completion = await StoreService.GetSetupCompletionAsync(storeId),
            Steps = steps
        });
    }
}
