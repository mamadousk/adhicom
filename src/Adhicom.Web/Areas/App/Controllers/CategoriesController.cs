using Adhicom.Application.Catalog;
using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Domain.Catalog;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.App.Controllers;

public class CategoriesController : MerchantControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;
    private readonly ICategoryService _categories;

    public CategoriesController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db, ICategoryService categories,
        IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        : base(storeService, tenant, currentUser, notifications, limits)
        => (_db, _categories) = (db, categories);

    public async Task<IActionResult> Index()
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var categories = await _db.Categories.Where(c => c.StoreId == CurrentStore.Id)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryListItemViewModel { Category = c, ProductCount = _db.Products.Count(p => p.CategoryId == c.Id) })
            .ToListAsync();
        ViewData["Categories"] = categories;
        return View(new CategoryFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        if (!ModelState.IsValid) return Redirect("/app/categories");
        var result = await _categories.CreateAsync(new Category
        {
            StoreId = CurrentStore.Id,
            Name = model.Name,
            Description = model.Description,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive
        });
        TempData["Toast"] = result.Success ? "Catégorie créée." : result.Error;
        return Redirect("/app/categories");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CategoryFormViewModel model)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var result = await _categories.UpdateAsync(new Category
        {
            Id = id,
            StoreId = CurrentStore.Id,
            Name = model.Name,
            Description = model.Description,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive
        });
        TempData["Toast"] = result.Success ? "Catégorie mise à jour." : result.Error;
        return Redirect("/app/categories");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var result = await _categories.DeleteAsync(id);
        TempData["Toast"] = result.Success ? "Catégorie supprimée." : result.Error;
        return Redirect("/app/categories");
    }
}
