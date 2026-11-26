using Adhicom.Application.Common;
using Adhicom.Application.Orders;
using Adhicom.Domain.Catalog;
using Adhicom.Domain.Common;
using Adhicom.Domain.Orders;
using Adhicom.Domain.Shipping;
using Adhicom.Domain.Stores;
using Adhicom.Infrastructure.Persistence;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.Storefront.Controllers;

/// <summary>
/// Boutiques publiques ({slug}.adhicom.com). Anonyme.
/// Le tenant est résolu depuis le slug de la route (vérifié en base), jamais depuis un cookie de session commerçant.
/// Le panier est cookie-based, lié à une boutique, jamais mutualisé entre boutiques.
/// </summary>
[Area("Storefront")]
[AllowAnonymous]
public class StorefrontController : Controller
{
    private const int PageSize = 24;

    private readonly AdhicomDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICartService _cart;
    private readonly IOrderService _orders;

    public StorefrontController(AdhicomDbContext db, ITenantContext tenant, ICartService cart, IOrderService orders)
        => (_db, _tenant, _cart, _orders) = (db, tenant, cart, orders);

    // ===================== Résolution boutique =====================
    private async Task<Store?> ResolveStoreAsync(string slug)
    {
        Store? store;
        using (_tenant.BypassScope())
            store = await _db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == slug.ToLowerInvariant());

        // À partir d'ici : toutes les requêtes tenant = cette boutique uniquement
        if (store is not null) _tenant.SetStore(store.Id, bypass: false);
        return store;
    }

    private async Task<StoreSettings?> LoadSettingsAsync(Guid storeId)
        => await _db.StoreSettings.AsNoTracking().FirstOrDefaultAsync(s => s.StoreId == storeId);

    private string CartCookieName(Guid storeId) => $"adhicom_cart_{storeId:N}";

    private string GetCartToken(Guid storeId)
    {
        var name = CartCookieName(storeId);
        if (Request.Cookies.TryGetValue(name, out var token) && token.Length is >= 16 and <= 64) return token;
        var fresh = Guid.NewGuid().ToString("N");
        Response.Cookies.Append(name, fresh, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddMonths(3), Secure = Request.IsHttps });
        return fresh;
    }

    private async Task SetCartCountAsync(Guid storeId, string token)
    {
        var cart = await _db.ShoppingCarts.AsNoTracking().Include(c => c.Items).FirstOrDefaultAsync(c => c.StoreId == storeId && c.Token == token);
        ViewData["CartCount"] = cart?.Items.Sum(i => i.Quantity) ?? 0;
    }

    private IActionResult? StoreUnavailableResult(Store store, StoreSettings? settings)
    {
        if (store.Status == StoreStatus.Active) return null;
        Response.StatusCode = StatusCodes.Status410Gone;
        ViewData["StoreStatus"] = store.Status;
        return View("Closed", store);
    }

    // ===================== Pages =====================
    public async Task<IActionResult> Index(string storeSlug, string? cat, string? q, int page = 1)
    {
        var store = await ResolveStoreAsync(storeSlug);
        if (store is null) { Response.StatusCode = StatusCodes.Status404NotFound; return View("StoreNotFound"); }
        var settings = await LoadSettingsAsync(store.Id);
        ViewData["Store"] = store;
        if (StoreUnavailableResult(store, settings) is { } unavailable) return unavailable;

        page = Math.Max(1, page);
        var productsQuery = _db.Products.AsNoTracking().Include(p => p.Images).Include(p => p.Category)
            .Where(p => p.StoreId == store.Id && p.Status == ProductStatus.Active);

        Category? activeCategory = null;
        if (!string.IsNullOrWhiteSpace(cat))
        {
            activeCategory = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.StoreId == store.Id && c.Slug == cat && c.IsActive);
            if (activeCategory is not null) productsQuery = productsQuery.Where(p => p.CategoryId == activeCategory.Id);
        }
        if (!string.IsNullOrWhiteSpace(q))
            productsQuery = productsQuery.Where(p => p.Name.Contains(q) || (p.Description != null && p.Description.Contains(q)));

        var total = await productsQuery.CountAsync();
        var products = await productsQuery.OrderByDescending(p => p.CreatedAtUtc).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        var categories = await _db.Categories.AsNoTracking().Where(c => c.StoreId == store.Id && c.IsActive).OrderBy(c => c.DisplayOrder).ToListAsync();

        var token = GetCartToken(store.Id);
        await SetCartCountAsync(store.Id, token);

        ViewData["Settings"] = settings;
        ViewData["Categories"] = categories;
        ViewData["Products"] = products;
        ViewData["ActiveCategory"] = activeCategory;
        ViewData["Query"] = q;
        ViewData["Page"] = page;
        ViewData["TotalPages"] = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        ViewData["Currency"] = settings?.Currency ?? "USD";
        return View(store);
    }

    public async Task<IActionResult> Product(string storeSlug, string productSlug)
    {
        var store = await ResolveStoreAsync(storeSlug);
        if (store is null) { Response.StatusCode = StatusCodes.Status404NotFound; return View("StoreNotFound"); }
        var settings = await LoadSettingsAsync(store.Id);
        ViewData["Store"] = store;
        if (StoreUnavailableResult(store, settings) is { } unavailable) return unavailable;

        var product = await _db.Products.AsNoTracking().Include(p => p.Images).Include(p => p.Variants).Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.StoreId == store.Id && p.Slug == productSlug && p.Status == ProductStatus.Active);
        if (product is null) { Response.StatusCode = StatusCodes.Status404NotFound; return View("StoreNotFound"); }

        var related = await _db.Products.AsNoTracking().Include(p => p.Images)
            .Where(p => p.StoreId == store.Id && p.Status == ProductStatus.Active && p.Id != product.Id && p.CategoryId == product.CategoryId)
            .OrderByDescending(p => p.CreatedAtUtc).Take(4).ToListAsync();

        var token = GetCartToken(store.Id);
        await SetCartCountAsync(store.Id, token);

        ViewData["Settings"] = settings;
        ViewData["Related"] = related;
        ViewData["ShareUrl"] = $"{Request.Scheme}://{Request.Host}/storefront/{store.Slug}/Product/{product.Slug}";
        return View(product);
    }

    [HttpGet]
    public async Task<IActionResult> Panier(string storeSlug)
    {
        var store = await ResolveStoreAsync(storeSlug);
        if (store is null) { Response.StatusCode = StatusCodes.Status404NotFound; return View("StoreNotFound"); }

        ViewData["Store"] = store;
        var token = GetCartToken(store.Id);
        var cart = await _cart.GetOrCreateAsync(store.Id, token);
        var vm = await BuildCartViewModelAsync(store, cart);
        await SetCartCountAsync(store.Id, token);
        ViewData["Settings"] = await LoadSettingsAsync(store.Id);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(string storeSlug, Guid productId, Guid? variantId, int quantity = 1)
    {
        var store = await ResolveStoreAsync(storeSlug);
        if (store is null) { Response.StatusCode = StatusCodes.Status404NotFound; return View("StoreNotFound"); }

        var token = GetCartToken(store.Id);
        var result = await _cart.AddItemAsync(store.Id, token, productId, variantId, quantity);
        TempData["Toast"] = result.Success ? "Produit ajouté au panier." : result.Error;
        TempData["ToastType"] = result.Success ? "success" : "error";
        return Redirect($"/storefront/{store.Slug}/Panier");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetQuantity(string storeSlug, Guid cartItemId, int quantity)
    {
        var store = await ResolveStoreAsync(storeSlug);
        if (store is null) return NotFound();
        var token = GetCartToken(store.Id);
        await _cart.SetQuantityAsync(store.Id, token, cartItemId, quantity);
        return Redirect($"/storefront/{store.Slug}/Panier");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveItem(string storeSlug, Guid cartItemId)
    {
        var store = await ResolveStoreAsync(storeSlug);
        if (store is null) return NotFound();
        var token = GetCartToken(store.Id);
        await _cart.RemoveItemAsync(store.Id, token, cartItemId);
        return Redirect($"/storefront/{store.Slug}/Panier");
    }

    [HttpGet]
    public async Task<IActionResult> Commander(string storeSlug)
    {
        var store = await ResolveStoreAsync(storeSlug);
        if (store is null) { Response.StatusCode = StatusCodes.Status404NotFound; return View("StoreNotFound"); }
        var settings = await LoadSettingsAsync(store.Id);
        if (StoreUnavailableResult(store, settings) is { } unavailable) return unavailable;

        ViewData["Store"] = store;
        var token = GetCartToken(store.Id);
        var cart = await _cart.GetOrCreateAsync(store.Id, token);
        if (cart.Items.Count == 0) return Redirect($"/storefront/{store.Slug}/Panier");

        var vm = await BuildCheckoutViewModelAsync(store, settings, cart);
        await SetCartCountAsync(store.Id, token);
        ViewData["Settings"] = settings;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Commander(string storeSlug, CheckoutInputModel form)
    {
        var store = await ResolveStoreAsync(storeSlug);
        if (store is null) { Response.StatusCode = StatusCodes.Status404NotFound; return View("StoreNotFound"); }
        var settings = await LoadSettingsAsync(store.Id);
        if (StoreUnavailableResult(store, settings) is { } unavailable) return unavailable;

        var token = GetCartToken(store.Id);
        var cart = await _cart.GetOrCreateAsync(store.Id, token);
        if (cart.Items.Count == 0) return Redirect($"/storefront/{store.Slug}/Panier");

        ViewData["Store"] = store;
        if (!ModelState.IsValid)
        {
            var vm = await BuildCheckoutViewModelAsync(store, settings, cart);
            vm.Form = form;
            ViewData["Settings"] = settings;
            return View(vm);
        }

        var command = new PlaceOrderCommand(store.Id, token, form.CustomerName, form.CustomerPhone, form.CustomerEmail,
            form.CustomerCity, form.CustomerDistrict, form.CustomerAddress, form.CustomerInstructions, form.PaymentMethod, form.ShippingMethodId);

        var result = await _orders.PlaceOrderAsync(command);
        ViewData["Store"] = store;
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            var vm = await BuildCheckoutViewModelAsync(store, settings, cart);
            vm.Form = form;
            ViewData["Settings"] = settings;
            return View(vm);
        }

        if (result.Value!.WhatsAppRedirectUrl is { } redirect)
            return Redirect(redirect); // Mode « redirection WhatsApp » : le client finalise la commande dans WhatsApp

        TempData["Toast"] = "Merci ! Votre commande est enregistrée.";
        return Redirect($"/storefront/{store.Slug}/Confirmation/{result.Value.OrderNumber}");
    }

    public async Task<IActionResult> Confirmation(string storeSlug, string orderNumber)
    {
        var store = await ResolveStoreAsync(storeSlug);
        if (store is null) { Response.StatusCode = StatusCodes.Status404NotFound; return View("StoreNotFound"); }

        var order = await _db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.StoreId == store.Id && o.OrderNumber == orderNumber);
        if (order is null) { Response.StatusCode = StatusCodes.Status404NotFound; return View("StoreNotFound"); }

        string? whatsApp = null;
        if (!string.IsNullOrWhiteSpace(store.WhatsappNumber))
        {
            var lines = string.Join(" | ", order.Items.Select(i => $"{i.ProductName} x{i.Quantity}"));
            whatsApp = WhatsAppLink.Build(store.WhatsappNumber, $"Bonjour {store.Name} ! Commande {order.OrderNumber} : {lines}. Total : {order.Total:N2} {order.Currency}. Nom : {order.CustomerName}.");
        }

        ViewData["Store"] = store;
        var settings = await LoadSettingsAsync(store.Id);
        ViewData["Settings"] = settings;
        await SetCartCountAsync(store.Id, GetCartToken(store.Id));
        return View(new ConfirmationViewModel { Store = store, Order = order, WhatsAppUrl = whatsApp, Currency = settings?.Currency ?? "USD" });
    }

    // ===================== Helpers =====================
    private async Task<CartViewModel> BuildCartViewModelAsync(Store store, ShoppingCart cart)
    {
        var lines = await BuildCartLinesAsync(store.Id, cart);
        var settings = await LoadSettingsAsync(store.Id);
        var (subTotal, _, _, _) = await _cart.ComputeTotalsAsync(store.Id, cart);
        return new CartViewModel { Store = store, Currency = settings?.Currency ?? "USD", Lines = lines, SubTotal = subTotal };
    }

    private async Task<CheckoutViewModel> BuildCheckoutViewModelAsync(Store store, StoreSettings? settings, ShoppingCart cart)
    {
        var lines = await BuildCartLinesAsync(store.Id, cart);
        var (subTotal, _, _, _) = await _cart.ComputeTotalsAsync(store.Id, cart);
        var methods = await _db.ShippingMethods.AsNoTracking().Where(m => m.StoreId == store.Id && m.IsActive).OrderBy(m => m.DisplayOrder).ToListAsync();
        var paymentConfigs = await _db.StorePaymentConfigurations.AsNoTracking().Where(c => c.StoreId == store.Id && c.IsEnabled).ToListAsync();

        return new CheckoutViewModel
        {
            Store = store,
            Currency = settings?.Currency ?? "USD",
            Lines = lines,
            SubTotal = subTotal,
            DeliveryInfo = settings?.DeliveryInfo,
            ShippingMethods = methods,
            PaymentConfigurations = paymentConfigs,
            Form = new CheckoutInputModel { PaymentMethod = paymentConfigs.FirstOrDefault()?.Method ?? PaymentMethods.CashOnDelivery, ShippingMethodId = methods.FirstOrDefault()?.Id }
        };
    }

    private async Task<List<CartLineViewModel>> BuildCartLinesAsync(Guid storeId, ShoppingCart cart)
    {
        var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.AsNoTracking().Include(p => p.Images)
            .Where(p => p.StoreId == storeId && productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var variants = await _db.ProductVariants.AsNoTracking().Where(v => v.StoreId == storeId).ToListAsync();

        var lines = new List<CartLineViewModel>();
        foreach (var item in cart.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product)) continue;
            var unit = product.Price;
            string? variantName = null;
            if (item.VariantId is { } vid)
            {
                var variant = variants.FirstOrDefault(v => v.Id == vid);
                if (variant is not null)
                {
                    if (variant.PriceAdjustment is { } adj) unit = adj;
                    variantName = $"{variant.Name} : {variant.Value}";
                }
            }
            lines.Add(new CartLineViewModel
            {
                CartItemId = item.Id,
                ProductId = product.Id,
                Name = product.Name,
                VariantName = variantName,
                ImageUrl = product.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                UnitPrice = unit,
                Quantity = item.Quantity
            });
        }
        return lines;
    }
}
