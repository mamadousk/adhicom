using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Domain.Common;
using Adhicom.Domain.Stores;
using Adhicom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Infrastructure.Services;

public sealed class EfStoreService : IStoreService
{
    private readonly AdhicomDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISubscriptionLimitService _limits;
    private readonly INotificationService _notifications;
    private readonly IClock _clock;

    public EfStoreService(AdhicomDbContext db, ITenantContext tenant, ISubscriptionLimitService limits, INotificationService notifications, IClock clock)
        => (_db, _tenant, _limits, _notifications, _clock) = (db, tenant, limits, notifications, clock);

    public async Task<OperationResult<Store>> CreateStoreAsync(CreateStoreRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return OperationResult<Store>.Fail("Le nom commercial de la boutique est requis.");
        if (request.Name.Trim().Length > 120) return OperationResult<Store>.Fail("Le nom commercial est trop long (120 caractères maximum).");

        // Limite du plan côté serveur
        var check = await _limits.CanCreateStoreAsync(request.OwnerId, ct);
        if (!check.Allowed) return OperationResult<Store>.Fail(check.Reason ?? "Limite de boutiques atteinte pour votre plan.");

        // Slug : demandé ou dérivé du nom, avec désambiguïsation auto si non demandé explicitement
        var requested = !string.IsNullOrWhiteSpace(request.RequestedSlug);
        var baseSlug = SlugHelper.Slugify(requested ? request.RequestedSlug : request.Name);
        if (!SlugHelper.IsValidSubdomain(baseSlug)) return OperationResult<Store>.Fail("Le sous-domaine doit contenir 3 à 48 caractères : lettres minuscules, chiffres et tirets (sans tiret en début/fin).");
        if (SlugHelper.IsReservedSubdomain(baseSlug)) return OperationResult<Store>.Fail("Ce sous-domaine est réservé par la plateforme. Veuillez en choisir un autre.");

        var slug = baseSlug;
        var suffix = 2;
        while (await _db.Stores.AsNoTracking().AnyAsync(s => s.Slug == slug, ct))
        {
            if (requested) return OperationResult<Store>.Fail($"Le sous-domaine « {baseSlug} » n'est plus disponible. Choisissez-en un autre.");
            slug = $"{baseSlug}-{suffix++}";
            if (suffix > 20) return OperationResult<Store>.Fail("Impossible de générer un sous-domaine disponible. Merci de préciser le nom d'adresse souhaité.");
        }

        var store = new Store
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            BusinessCategory = request.BusinessCategory?.Trim(),
            Country = string.IsNullOrWhiteSpace(request.Country) ? "CD" : request.Country.Trim().ToUpperInvariant(),
            City = request.City?.Trim(),
            Address = request.Address?.Trim(),
            WhatsappNumber = request.WhatsappNumber?.Trim(),
            BusinessEmail = request.BusinessEmail?.Trim(),
            OwnerId = request.OwnerId,
            Status = StoreStatus.Active,
        };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.Stores.Add(store);
            _db.StoreSettings.Add(new StoreSettings { StoreId = store.Id, Currency = Currencies.Usd, Timezone = "Africa/Kinshasa" });
            _db.StoreDomains.Add(new StoreDomain { StoreId = store.Id, Domain = $"{slug}.adhicom.com", IsPrimary = true, IsCustom = false, IsVerified = true, VerifiedAtUtc = _clock.UtcNow });
            _db.StoreMembers.Add(new StoreMember { StoreId = store.Id, UserId = request.OwnerId, Role = "owner" });
            // Moyens de paiement par défaut : paiement à la livraison et paiement manuel activés
            _db.StorePaymentConfigurations.Add(new Adhicom.Domain.Payments.StorePaymentConfiguration { StoreId = store.Id, Method = PaymentMethods.CashOnDelivery, IsEnabled = true });
            _db.StorePaymentConfigurations.Add(new Adhicom.Domain.Payments.StorePaymentConfiguration { StoreId = store.Id, Method = PaymentMethods.Manual, IsEnabled = true });
            // Retrait sur place par défaut
            _db.ShippingMethods.Add(new Adhicom.Domain.Shipping.ShippingMethod { StoreId = store.Id, Name = "Retrait sur place", Type = ShippingMethodType.Pickup, Price = 0, EstimatedDelivery = "Selon votre disponibilité" });
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        await _notifications.NotifyAsync(request.OwnerId, store.Id, "store", "Bienvenue sur Adhicom 🎉",
            $"Votre boutique « {store.Name} » a été créée. Son adresse publique : {slug}.adhicom.com", "/app", ct);

        return OperationResult<Store>.Ok(store);
    }

    public async Task<bool> IsSlugAvailableAsync(string slug, CancellationToken ct = default)
    {
        var normalized = (slug ?? string.Empty).Trim().ToLowerInvariant();
        if (!SlugHelper.IsValidSubdomain(normalized) || SlugHelper.IsReservedSubdomain(normalized)) return false;
        return !await _db.Stores.AsNoTracking().AnyAsync(s => s.Slug == normalized, ct);
    }

    public async Task<IReadOnlyList<Store>> ListMyStoresAsync(Guid userId, CancellationToken ct = default)
    {
        using (_tenant.BypassScope())
        {
            var ids = await _db.StoreMembers.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.StoreId).ToListAsync(ct);
            return await _db.Stores.AsNoTracking().Where(s => ids.Contains(s.Id)).OrderByDescending(s => s.CreatedAtUtc).ToListAsync(ct);
        }
    }

    public async Task<bool> IsMemberAsync(Guid userId, Guid storeId, CancellationToken ct = default)
    {
        using (_tenant.BypassScope())
            return await _db.StoreMembers.AsNoTracking().AnyAsync(m => m.StoreId == storeId && m.UserId == userId, ct);
    }

    public async Task<OperationResult> UpdateProfileAsync(Guid storeId, Store profile, CancellationToken ct = default)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == storeId, ct);
        if (store is null) return OperationResult.Fail("Boutique introuvable.");

        store.Name = profile.Name?.Trim() ?? store.Name;
        store.Description = profile.Description?.Trim();
        store.BusinessCategory = profile.BusinessCategory?.Trim();
        store.City = profile.City?.Trim();
        store.Country = string.IsNullOrWhiteSpace(profile.Country) ? store.Country : profile.Country.Trim().ToUpperInvariant();
        store.Address = profile.Address?.Trim();
        store.WhatsappNumber = profile.WhatsappNumber?.Trim();
        store.BusinessEmail = profile.BusinessEmail?.Trim();
        store.ThemeKey = profile.ThemeKey?.Trim() ?? store.ThemeKey;
        store.PrimaryColor = NormalizeColor(profile.PrimaryColor, store.PrimaryColor);
        store.SecondaryColor = NormalizeColor(profile.SecondaryColor, store.SecondaryColor);
        store.FontFamily = profile.FontFamily?.Trim() ?? store.FontFamily;
        store.LogoUrl = profile.LogoUrl ?? store.LogoUrl;
        store.BannerUrl = profile.BannerUrl ?? store.BannerUrl;
        store.StockMode = profile.StockMode;
        store.WhatsAppMode = profile.WhatsAppMode;
        store.UpdatedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    private static string NormalizeColor(string? input, string fallback)
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;
        var v = input.Trim();
        return System.Text.RegularExpressions.Regex.IsMatch(v, "^#[0-9a-fA-F]{6}$") ? v.ToUpperInvariant() : fallback;
    }

    public async Task<OperationResult> UpdateSettingsAsync(Guid storeId, StoreSettings settings, CancellationToken ct = default)
    {
        var entity = await _db.StoreSettings.FirstOrDefaultAsync(s => s.StoreId == storeId, ct);
        if (entity is null)
        {
            entity = new StoreSettings { StoreId = storeId };
            _db.StoreSettings.Add(entity);
        }
        entity.Currency = Currencies.All.Contains(settings.Currency) ? settings.Currency : Currencies.Usd;
        entity.Timezone = string.IsNullOrWhiteSpace(settings.Timezone) ? "Africa/Kinshasa" : settings.Timezone;
        entity.SeoTitle = settings.SeoTitle?.Trim();
        entity.SeoDescription = settings.SeoDescription?.Trim();
        entity.SocialInstagram = settings.SocialInstagram?.Trim();
        entity.SocialFacebook = settings.SocialFacebook?.Trim();
        entity.SocialTiktok = settings.SocialTiktok?.Trim();
        entity.SocialYoutube = settings.SocialYoutube?.Trim();
        entity.ContactPhone = settings.ContactPhone?.Trim();
        entity.DeliveryInfo = settings.DeliveryInfo?.Trim();
        entity.FreeShippingOver = settings.FreeShippingOver;
        entity.BannerText = settings.BannerText?.Trim();
        entity.ShowBanner = settings.ShowBanner;
        entity.LowStockThreshold = Math.Clamp(settings.LowStockThreshold, 0, 10000);
        entity.UpdatedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    public async Task<int> GetSetupCompletionAsync(Guid storeId, CancellationToken ct = default)
    {
        // 8 étapes : logo, thème, couleurs, catégories, produits, paiements, livraison, publication
        var store = await _db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId, ct);
        if (store is null) return 0;
        using (_tenant.BypassScope())
        {
            var done = 0;
            if (!string.IsNullOrWhiteSpace(store.LogoUrl)) done++;
            if (!string.IsNullOrWhiteSpace(store.ThemeKey)) done++;
            if (!string.IsNullOrWhiteSpace(store.PrimaryColor) && store.PrimaryColor != "#0F4C3A") done++;
            if (await _db.Categories.AnyAsync(c => c.StoreId == storeId, ct)) done++;
            if (await _db.Products.AnyAsync(p => p.StoreId == storeId, ct)) done++;
            if (await _db.StorePaymentConfigurations.AnyAsync(p => p.StoreId == storeId && p.IsEnabled, ct)) done++;
            if (await _db.ShippingMethods.AnyAsync(m => m.StoreId == storeId && m.IsActive, ct)) done++;
            if (store.Status == StoreStatus.Active) done++;
            return (int)Math.Round(done * 100.0 / 8.0);
        }
    }
}
