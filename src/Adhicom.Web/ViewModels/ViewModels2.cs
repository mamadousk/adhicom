using System.ComponentModel.DataAnnotations;
using Adhicom.Domain.Common;
using Adhicom.Domain.Orders;
using Adhicom.Domain.Payments;
using Adhicom.Domain.Shipping;
using Adhicom.Domain.Stores;
using Adhicom.Domain.Subscriptions;

namespace Adhicom.Web.ViewModels;

// ===================== Espace commerçant (suite) =====================
public class DashboardViewModel
{
    public Store Store { get; set; } = null!;
    public Adhicom.Application.Stats.MerchantDashboardDto Stats { get; set; } = null!;
    public int SetupCompletion { get; set; }
    public string Currency { get; set; } = "USD";
}

public class StoresIndexViewModel
{
    public List<Store> Stores { get; set; } = new();
    public Guid? ActiveStoreId { get; set; }
    public int MaxStores { get; set; }
}

public class SubscriptionViewModel
{
    public Subscription? Subscription { get; set; }
    public Plan Plan { get; set; } = null!;
    public int ProductsUsed { get; set; }
    public int ProductsLimit { get; set; }
    public int StoresUsed { get; set; }
    public int StoresLimit { get; set; }
    public bool AdvancedAnalytics { get; set; }
    public int DaysRemaining { get; set; }
    public List<Plan> AvailablePlans { get; set; } = new();
}

public class ShippingMethodFormViewModel
{
    [Required(ErrorMessage = "Le nom est requis.")]
    [StringLength(120)]
    [Display(Name = "Nom (ex. Livraison Kinshasa 24–48h)")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Type")]
    public ShippingMethodType Type { get; set; } = ShippingMethodType.Flat;

    [Range(0, 100000)]
    [Display(Name = "Prix")]
    public decimal Price { get; set; }

    [Range(0, 1000000)]
    [Display(Name = "Gratuit au-delà de (facultatif)")]
    public decimal? FreeOverAmount { get; set; }

    [StringLength(120)]
    [Display(Name = "Délai indicatif")]
    public string? EstimatedDelivery { get; set; }
}

public class PaymentMethodsFormViewModel
{
    public List<StorePaymentConfiguration> Configurations { get; set; } = new();
}

public class SetupStep
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Done { get; set; }
    public string Link { get; set; } = "/app";
}

public class OnboardingViewModel
{
    public Store Store { get; set; } = null!;
    public int Completion { get; set; }
    public List<SetupStep> Steps { get; set; } = new();
}

// ===================== Administration =====================
public class AdminDashboardViewModel
{
    public Adhicom.Application.Stats.AdminDashboardDto Stats { get; set; } = null!;
}

public class AdminStoreRowViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public StoreStatus Status { get; set; }
    public string OwnerEmail { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public int OrderCount { get; set; }
}

public class AdminStoresViewModel
{
    public List<AdminStoreRowViewModel> Stores { get; set; } = new();
    public string? StatusFilter { get; set; }
    public string? Search { get; set; }
}

public class AdminUserRowViewModel
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public int StoreCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AdminUsersViewModel
{
    public List<AdminUserRowViewModel> Users { get; set; } = new();
    public string? Search { get; set; }
}

public class PlanFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(32)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    [Display(Name = "Nom du plan")]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Range(0, 1000000)]
    [Display(Name = "Prix mensuel")]
    public decimal MonthlyPrice { get; set; }

    [StringLength(3)]
    public string Currency { get; set; } = "USD";

    public bool IsActive { get; set; } = true;

    [Range(1, 100)]
    [Display(Name = "Ordre d'affichage")]
    public int SortOrder { get; set; } = 1;

    [Range(1, 100)]
    [Display(Name = "Boutiques max")]
    public int MaxStores { get; set; } = 1;

    [Range(1, 1000000)]
    [Display(Name = "Produits max")]
    public int MaxProducts { get; set; } = 20;

    [Range(1, 1000)]
    [Display(Name = "Utilisateurs max")]
    public int MaxUsers { get; set; } = 1;

    [Range(10, 1000000)]
    [Display(Name = "Espace de stockage (Mo)")]
    public int MaxStorageMb { get; set; } = 100;

    public bool AllowPremiumThemes { get; set; }
    public bool AllowAdvancedAnalytics { get; set; }
    public bool AllowCustomDomain { get; set; }
    public bool AllowMarketing { get; set; }
    public bool AllowCoupons { get; set; }
    public bool AllowExports { get; set; }
}

public class AdminSubscriptionRowViewModel
{
    public Guid Id { get; set; }
    public string SubscriberEmail { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "USD";
}

public class AdminSubscriptionsViewModel
{
    public List<AdminSubscriptionRowViewModel> Subscriptions { get; set; } = new();
    public List<Plan> Plans { get; set; } = new();
    public string? StatusFilter { get; set; }
}

public class SupportTicketViewModel
{
    public List<Adhicom.Domain.Administration.SupportTicket> OpenTickets { get; set; } = new();
    public List<Adhicom.Domain.Administration.SupportTicket> ClosedTickets { get; set; } = new();
}

// ===================== Boutique publique =====================
public class CartLineViewModel
{
    public Guid CartItemId { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class CartViewModel
{
    public Store Store { get; set; } = null!;
    public string Currency { get; set; } = "USD";
    public List<CartLineViewModel> Lines { get; set; } = new();
    public decimal SubTotal { get; set; }
    public int TotalItems => Lines.Sum(l => l.Quantity);
}

public class CheckoutInputModel
{
    [Required(ErrorMessage = "Votre nom est requis.")]
    [StringLength(160)]
    [Display(Name = "Nom et prénom")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Votre numéro de téléphone est requis.")]
    [Phone]
    [StringLength(32)]
    [Display(Name = "Téléphone (le livreur vous appellera)")]
    public string CustomerPhone { get; set; } = string.Empty;

    [EmailAddress]
    [Display(Name = "E-mail (facultatif)")]
    public string? CustomerEmail { get; set; }

    [Display(Name = "Ville")]
    public string? CustomerCity { get; set; }

    [Display(Name = "Commune / Quartier")]
    public string? CustomerDistrict { get; set; }

    [Display(Name = "Adresse ou point de livraison")]
    public string? CustomerAddress { get; set; }

    [StringLength(500)]
    [Display(Name = "Instructions complémentaires (facultatif)")]
    public string? CustomerInstructions { get; set; }

    [Required(ErrorMessage = "Choisissez un moyen de paiement.")]
    [Display(Name = "Moyen de paiement")]
    public string PaymentMethod { get; set; } = string.Empty;

    [Display(Name = "Mode de livraison")]
    public Guid? ShippingMethodId { get; set; }
}

public class CheckoutViewModel
{
    public Store Store { get; set; } = null!;
    public string Currency { get; set; } = "USD";
    public List<CartLineViewModel> Lines { get; set; } = new();
    public decimal SubTotal { get; set; }
    public string? DeliveryInfo { get; set; }
    public List<ShippingMethod> ShippingMethods { get; set; } = new();
    public List<StorePaymentConfiguration> PaymentConfigurations { get; set; } = new();
    public CheckoutInputModel Form { get; set; } = new();
}

public class ConfirmationViewModel
{
    public Store Store { get; set; } = null!;
    public Order Order { get; set; } = null!;
    public string? WhatsAppUrl { get; set; }
    public string Currency { get; set; } = "USD";
}

namespace Adhicom.Web.ViewModels;

public class SettingsPageViewModel
{
    public Store Store { get; set; } = null!;
    public ProfileSettingsViewModel Profile { get; set; } = new();
    public AppearanceSettingsViewModel Appearance { get; set; } = new();
    public SeoSettingsViewModel Seo { get; set; } = new();
}
