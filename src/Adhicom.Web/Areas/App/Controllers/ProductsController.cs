using Adhicom.Application.Catalog;
using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Domain.Catalog;
using Adhicom.Domain.Common;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.App.Controllers;

public class ProductsController : MerchantControllerBase
{
    private const int PageSize = 20;

    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;
    private readonly IProductService _products;
    private readonly IFileStorage _files;

    public ProductsController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db, IProductService products, IFileStorage files,
        IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        : base(storeService, tenant, currentUser, notifications, limits)
        => (_db, _products, _files) = (db, products, files);

    public async Task<IActionResult> Index(string? q, string? status, int page = 1)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var storeId = CurrentStore.Id;
        page = Math.Max(1, page);

        var query = _db.Products.Include(p => p.Category).Include(p => p.Images).Where(p => p.StoreId == storeId).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => p.Name.Contains(q) || (p.Sku != null && p.Sku.Contains(q)));
        if (Enum.TryParse<ProductStatus>(status, true, out var parsedStatus)) query = query.Where(p => p.Status == parsedStatus);
        else query = query.Where(p => p.Status != ProductStatus.Archived);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.CreatedAtUtc).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

        ViewData["Query"] = q;
        ViewData["Status"] = status;
        ViewData["Page"] = page;
        ViewData["TotalPages"] = (int)Math.Ceiling(total / (double)PageSize);
        ViewData["Total"] = total;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        await LoadCategoriesAsync();
        return View(new ProductFormViewModel { Status = ProductStatus.Active });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, IFormFileCollection? images)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        if (!ModelState.IsValid) { await LoadCategoriesAsync(); return View(model); }

        var product = new Product
        {
            StoreId = CurrentStore.Id,
            Name = model.Name,
            Description = model.Description,
            Price = model.Price,
            CompareAtPrice = model.CompareAtPrice,
            Sku = model.Sku,
            CategoryId = model.CategoryId,
            Status = model.Status,
            StockQuantity = model.StockQuantity,
            WeightKg = model.WeightKg
        };

        var result = await _products.CreateAsync(product);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadCategoriesAsync();
            return View(model);
        }

        await SaveImagesAsync(result.Value!.Id, images);
        TempData["Toast"] = "Produit créé.";
        return Redirect("/app/products");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id && p.StoreId == CurrentStore.Id);
        if (product is null) return NotFound();

        await LoadCategoriesAsync();
        ViewData["Images"] = product.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).ToList();
        return View(new ProductFormViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            CompareAtPrice = product.CompareAtPrice,
            Sku = product.Sku,
            CategoryId = product.CategoryId,
            Status = product.Status,
            StockQuantity = product.StockQuantity,
            WeightKg = product.WeightKg
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ProductFormViewModel model, IFormFileCollection? images)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync();
            ViewData["Images"] = await _db.ProductImages.Where(i => i.ProductId == id).OrderByDescending(i => i.IsPrimary).ToListAsync();
            return View(model);
        }

        model.Id = id;
        var product = new Product { StoreId = CurrentStore.Id, Name = model.Name, Description = model.Description, Price = model.Price, CompareAtPrice = model.CompareAtPrice, Sku = model.Sku, CategoryId = model.CategoryId, Status = model.Status, StockQuantity = model.StockQuantity, WeightKg = model.WeightKg };
        product.Id = id;

        var result = await _products.UpdateAsync(product);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadCategoriesAsync();
            return View(model);
        }

        await SaveImagesAsync(id, images);
        TempData["Toast"] = "Produit mis à jour.";
        return Redirect("/app/products");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        await _products.ArchiveAsync(id);
        TempData["Toast"] = "Produit archivé (le produit reste visible dans l'historique des commandes).";
        return Redirect("/app/products");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(Guid id)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.StoreId == CurrentStore.Id);
        if (product is not null)
            await _products.SetStatusAsync(id, product.Status == ProductStatus.Active ? ProductStatus.Draft : ProductStatus.Active);
        return Redirect("/app/products");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryImage(Guid productId, Guid imageId)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var images = await _db.ProductImages.Where(i => i.ProductId == productId && i.StoreId == CurrentStore.Id).ToListAsync();
        if (images.All(i => i.ProductId == productId))
        {
            foreach (var img in images) img.IsPrimary = img.Id == imageId;
            await _db.SaveChangesAsync();
        }
        return Redirect($"/app/products/edit/{productId}");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(Guid productId, Guid imageId)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var image = await _db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId && i.StoreId == CurrentStore.Id);
        if (image is not null)
        {
            _db.ProductImages.Remove(image);
            await _db.SaveChangesAsync();
            try { await _files.DeleteAsync(image.Url); } catch { /* fichier déjà absent : ignoré */ }
            TempData["Toast"] = "Image supprimée.";
        }
        return Redirect($"/app/products/edit/{productId}");
    }

    private async Task SaveImagesAsync(Guid productId, IFormFileCollection? images)
    {
        if (images is null) return;
        var hasPrimary = await _db.ProductImages.AnyAsync(i => i.ProductId == productId && i.IsPrimary);
        var order = await _db.ProductImages.Where(i => i.ProductId == productId).CountAsync();
        foreach (var file in images.Take(8)) // 8 images max par produit
        {
            if (file.Length == 0) continue;
            if (file.Length > 4 * 1024 * 1024) { TempData["Toast"] = $"Image « {file.FileName} » ignorée : 4 Mo maximum."; continue; }
            try
            {
                using var stream = file.OpenReadStream();
                var url = await _files.SaveAsync(stream, file.FileName, file.ContentType, $"stores/{CurrentStore!.Id}");
                _db.ProductImages.Add(new ProductImage { StoreId = CurrentStore.Id, ProductId = productId, Url = url, IsPrimary = !hasPrimary, DisplayOrder = order++ });
                hasPrimary = true;
            }
            catch (InvalidOperationException ex)
            {
                TempData["Toast"] = ex.Message;
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task LoadCategoriesAsync()
        => ViewData["Categories"] = new SelectList(await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync(), "Id", "Name");
}
