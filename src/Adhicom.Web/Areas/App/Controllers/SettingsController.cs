using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Adhicom.Web.Areas.App.Controllers;

public class SettingsController : MerchantControllerBase
{
    public SettingsController(IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        : base(storeService, tenant, currentUser, notifications, limits) { }

    public async Task<IActionResult> Index()
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        TempData.Remove("Toast");
        var (profile, appearance, seo) = await LoadSettings();
        return View(new SettingsPageViewModel { Store = CurrentStore, Profile = profile, Appearance = appearance, Seo = seo });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(ProfileSettingsViewModel model)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        if (!ModelState.IsValid) { TempData["Toast"] = "Vérifiez les champs du formulaire."; TempData["ToastType"] = "error"; return Redirect("/app/settings"); }

        var profile = new Adhicom.Domain.Stores.Store
        {
            Id = CurrentStore.Id,
            Name = model.Name,
            Slug = CurrentStore.Slug,
            Description = model.Description,
            BusinessCategory = model.BusinessCategory,
            Country = model.Country,
            City = model.City,
            Address = model.Address,
            WhatsappNumber = model.WhatsappNumber,
            BusinessEmail = model.BusinessEmail,
            StockMode = model.StockMode,
            WhatsAppMode = model.WhatsAppMode
        };
        await StoreService.UpdateProfileAsync(CurrentStore.Id, profile);
        TempData["Toast"] = "Profil de la boutique enregistré.";
        return Redirect("/app/settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAppearance(AppearanceSettingsViewModel model)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        if (!ModelState.IsValid) { TempData["Toast"] = "Couleurs invalides (format #RRGGBB)."; TempData["ToastType"] = "error"; return Redirect("/app/settings#apparence"); }

        var profile = new Adhicom.Domain.Stores.Store
        {
            Id = CurrentStore.Id,
            Name = CurrentStore.Name,
            Slug = CurrentStore.Slug,
            ThemeKey = model.ThemeKey,
            PrimaryColor = model.PrimaryColor,
            SecondaryColor = model.SecondaryColor,
            FontFamily = model.FontFamily
        };
        await StoreService.UpdateProfileAsync(CurrentStore.Id, profile);

        var (_, _, seo) = await LoadSettings();
        var entity = ToSettingsEntity(seo);
        entity.BannerText = model.BannerText;
        entity.ShowBanner = model.ShowBanner;
        await StoreService.UpdateSettingsAsync(CurrentStore.Id, entity);

        TempData["Toast"] = "Apparence mise à jour.";
        return Redirect("/app/settings#apparence");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSeo(SeoSettingsViewModel model)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var entity = ToSettingsEntity(model);
        await StoreService.UpdateSettingsAsync(CurrentStore.Id, entity);
        TempData["Toast"] = "Paramètres enregistrés.";
        return Redirect("/app/settings#seo");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadLogo(IFormFile? logo)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        if (logo is null || logo.Length == 0) { TempData["Toast"] = "Aucun fichier sélectionné."; TempData["ToastType"] = "error"; return Redirect("/app/settings"); }
        try
        {
            var files = HttpContext.RequestServices.GetRequiredService<IFileStorage>();
            using var stream = logo.OpenReadStream();
            var url = await files.SaveAsync(stream, logo.FileName, logo.ContentType, $"stores/{CurrentStore.Id}");
            var current = new Adhicom.Domain.Stores.Store
            {
                Id = CurrentStore.Id,
                Name = CurrentStore.Name,
                Slug = CurrentStore.Slug,
                LogoUrl = url
            };
            await StoreService.UpdateProfileAsync(CurrentStore.Id, current);
            TempData["Toast"] = "Logo mis à jour.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Toast"] = ex.Message;
            TempData["ToastType"] = "error";
        }
        return Redirect("/app/settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBanner(IFormFile? banner)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        if (banner is null || banner.Length == 0) { TempData["Toast"] = "Aucun fichier sélectionné."; TempData["ToastType"] = "error"; return Redirect("/app/settings#apparence"); }
        try
        {
            var files = HttpContext.RequestServices.GetRequiredService<IFileStorage>();
            using var stream = banner.OpenReadStream();
            var url = await files.SaveAsync(stream, banner.FileName, banner.ContentType, $"stores/{CurrentStore.Id}");
            var current = new Adhicom.Domain.Stores.Store
            {
                Id = CurrentStore.Id,
                Name = CurrentStore.Name,
                Slug = CurrentStore.Slug,
                BannerUrl = url
            };
            await StoreService.UpdateProfileAsync(CurrentStore.Id, current);
            TempData["Toast"] = "Bannière mise à jour.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Toast"] = ex.Message;
            TempData["ToastType"] = "error";
        }
        return Redirect("/app/settings#apparence");
    }

    private Adhicom.Domain.Stores.StoreSettings ToSettingsEntity(SeoSettingsViewModel model) => new()
    {
        StoreId = CurrentStore!.Id,
        Currency = model.Currency,
        Timezone = model.Timezone,
        SeoTitle = model.SeoTitle,
        SeoDescription = model.SeoDescription,
        SocialInstagram = model.SocialInstagram,
        SocialFacebook = model.SocialFacebook,
        SocialTiktok = model.SocialTiktok,
        SocialYoutube = model.SocialYoutube,
        DeliveryInfo = model.DeliveryInfo,
        ContactPhone = model.ContactPhone,
        FreeShippingOver = model.FreeShippingOver,
        LowStockThreshold = model.LowStockThreshold
    };

    private async Task<(ProfileSettingsViewModel Profile, AppearanceSettingsViewModel Appearance, SeoSettingsViewModel Seo)> LoadSettings()
    {
        var db = HttpContext.RequestServices.GetRequiredService<Adhicom.Infrastructure.Persistence.AdhicomDbContext>();
        var s = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.StoreSettings.Where(x => x.StoreId == CurrentStore!.Id));

        var profile = new ProfileSettingsViewModel
        {
            Name = CurrentStore!.Name,
            Description = CurrentStore.Description,
            BusinessCategory = CurrentStore.BusinessCategory,
            Country = CurrentStore.Country,
            City = CurrentStore.City,
            Address = CurrentStore.Address,
            WhatsappNumber = CurrentStore.WhatsappNumber,
            BusinessEmail = CurrentStore.BusinessEmail,
            StockMode = CurrentStore.StockMode,
            WhatsAppMode = CurrentStore.WhatsAppMode
        };
        var appearance = new AppearanceSettingsViewModel
        {
            ThemeKey = CurrentStore.ThemeKey,
            PrimaryColor = CurrentStore.PrimaryColor,
            SecondaryColor = CurrentStore.SecondaryColor,
            FontFamily = CurrentStore.FontFamily,
            BannerText = s?.BannerText,
            ShowBanner = s?.ShowBanner ?? true
        };
        var seo = new SeoSettingsViewModel
        {
            Currency = s?.Currency ?? "USD",
            Timezone = s?.Timezone ?? "Africa/Kinshasa",
            SeoTitle = s?.SeoTitle,
            SeoDescription = s?.SeoDescription,
            SocialInstagram = s?.SocialInstagram,
            SocialFacebook = s?.SocialFacebook,
            SocialTiktok = s?.SocialTiktok,
            SocialYoutube = s?.SocialYoutube,
            DeliveryInfo = s?.DeliveryInfo,
            ContactPhone = s?.ContactPhone,
            FreeShippingOver = s?.FreeShippingOver,
            LowStockThreshold = s?.LowStockThreshold ?? 5
        };
        return (profile, appearance, seo);
    }
}
