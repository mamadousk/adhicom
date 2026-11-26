using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Domain.Payments;
using Adhicom.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.App.Controllers;

public class PaymentMethodsController : MerchantControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;

    public PaymentMethodsController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db,
        IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        : base(storeService, tenant, currentUser, notifications, limits)
        => _db = db;

    public async Task<IActionResult> Index()
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var configs = await EnsureConfigurationsAsync();
        ViewData["Methods"] = BuildRows(configs);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string method, bool enabled, string? publicInstructions, string? publicMerchantCode)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        if (!PaymentMethods.All.Split(',').Contains(method)) return BadRequest();

        var configs = await EnsureConfigurationsAsync();
        var config = configs.First(c => c.Method == method);
        config.IsEnabled = enabled;
        config.PublicInstructions = string.IsNullOrWhiteSpace(publicInstructions) ? null : publicInstructions.Trim();
        config.PublicMerchantCode = string.IsNullOrWhiteSpace(publicMerchantCode) ? null : publicMerchantCode.Trim();
        config.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Moyens de paiement enregistrés.";
        return Redirect("/app/settings/payments");
    }

    private async Task<List<StorePaymentConfiguration>> EnsureConfigurationsAsync()
    {
        var storeId = CurrentStore!.Id;
        var configs = await _db.StorePaymentConfigurations.Where(c => c.StoreId == storeId).ToListAsync();
        var known = PaymentMethods.All.Split(',');
        foreach (var method in known)
        {
            if (!configs.Any(c => c.Method == method))
            {
                var config = new StorePaymentConfiguration
                {
                    StoreId = storeId,
                    Method = method,
                    IsEnabled = method is PaymentMethods.CashOnDelivery or PaymentMethods.Manual
                };
                _db.StorePaymentConfigurations.Add(config);
                configs.Add(config);
            }
        }
        await _db.SaveChangesAsync();
        return configs;
    }

    private static List<(StorePaymentConfiguration Config, string Title, string Description)> BuildRows(List<StorePaymentConfiguration> configs) => new()
    {
        Row(configs, PaymentMethods.CashOnDelivery, "Paiement à la livraison", "Votre client paie en espèces à la remise de la commande."),
        Row(configs, PaymentMethods.Manual, "Paiement manuel (Mobile Money / virement)", "Le client paie hors ligne et envoie la preuve ; vous validez la réception dans le tableau de bord."),
        Row(configs, PaymentMethods.BankTransfer, "Virement bancaire", "Instructions de virement affichées au client ; confirmation manuelle."),
        Row(configs, PaymentMethods.MobileMoney, "Mobile Money automatisé", "Moko Afrika, Nomba, OPTGateway, Malipo — activation après validation du prestataire (KYC, sous-comptes marchands)."),
        Row(configs, PaymentMethods.Card, "Carte bancaire", "Paiement par carte via passerelle — activation après validation du prestataire."),
    };

    private static (StorePaymentConfiguration Config, string Title, string Description) Row(List<StorePaymentConfiguration> configs, string method, string title, string description)
        => (configs.First(c => c.Method == method), title, description);
}
