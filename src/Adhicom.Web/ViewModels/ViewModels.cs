using System.ComponentModel.DataAnnotations;
using Adhicom.Domain.Common;
using Adhicom.Domain.Orders;
using Adhicom.Domain.Stores;

namespace Adhicom.Web.ViewModels;

// ===================== Compte =====================
public class LoginViewModel
{
    [Required(ErrorMessage = "L'adresse e-mail est requise.")]
    [EmailAddress]
    [Display(Name = "Adresse e-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est requis.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Se souvenir de moi")]
    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Le prénom est requis.")]
    [StringLength(80)]
    [Display(Name = "Prénom")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est requis.")]
    [StringLength(80)]
    [Display(Name = "Nom")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'adresse e-mail est requise.")]
    [EmailAddress]
    [Display(Name = "Adresse e-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le numéro de téléphone est requis.")]
    [Phone]
    [StringLength(32)]
    [Display(Name = "Téléphone (WhatsApp)")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est requis.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "8 caractères minimum.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmation est requise.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    [Display(Name = "Confirmation du mot de passe")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Range(typeof(bool), "true", "true", ErrorMessage = "Vous devez accepter les conditions d'utilisation.")]
    [Display(Name = "J'accepte les Conditions Générales d'Utilisation et la Politique de Confidentialité")]
    public bool AcceptTerms { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "L'adresse e-mail est requise.")]
    [EmailAddress]
    [Display(Name = "Adresse e-mail")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est requis.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "8 caractères minimum.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nouveau mot de passe")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    [Display(Name = "Confirmation")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// ===================== Site public =====================
public class ContactViewModel
{
    [Required(ErrorMessage = "Votre nom est requis.")]
    [StringLength(120)]
    [Display(Name = "Nom complet")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Votre e-mail est requis.")]
    [EmailAddress]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le sujet est requis.")]
    [StringLength(200)]
    [Display(Name = "Sujet")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Votre message est requis.")]
    [StringLength(5000)]
    [Display(Name = "Message")]
    public string Message { get; set; } = string.Empty;
}

// ===================== Espace commerçant =====================
public class CreateStoreViewModel
{
    [Required(ErrorMessage = "Le nom commercial est requis.")]
    [StringLength(120)]
    [Display(Name = "Nom commercial")]
    public string Name { get; set; } = string.Empty;

    [StringLength(48)]
    [RegularExpression("^[a-z0-9]([a-z0-9-]*[a-z0-9])?$", ErrorMessage = "Lettres minuscules, chiffres et tirets uniquement (3–48 caractères).")]
    [Display(Name = "Nom dans l'adresse (sous-domaine)")]
    public string? RequestedSlug { get; set; }

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(80)]
    [Display(Name = "Catégorie d'activité")]
    public string? BusinessCategory { get; set; }

    [Required]
    [StringLength(2, MinimumLength = 2)]
    [Display(Name = "Pays (code ISO)")]
    public string Country { get; set; } = "CD";

    [StringLength(80)]
    [Display(Name = "Ville")]
    public string? City { get; set; }

    [StringLength(200)]
    [Display(Name = "Adresse commerciale (facultatif)")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Le numéro WhatsApp est requis.")]
    [StringLength(32)]
    [Display(Name = "Numéro WhatsApp (format international)")]
    public string WhatsappNumber { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(160)]
    [Display(Name = "E-mail professionnel (facultatif)")]
    public string? BusinessEmail { get; set; }
}

public class ProductFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Le nom du produit est requis.")]
    [StringLength(200)]
    [Display(Name = "Nom du produit")]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Range(0, 10000000, ErrorMessage = "Prix invalide.")]
    [Display(Name = "Prix de vente")]
    public decimal Price { get; set; }

    [Range(0, 10000000, ErrorMessage = "Prix promotionnel invalide.")]
    [Display(Name = "Prix promotionnel (facultatif)")]
    public decimal? CompareAtPrice { get; set; }

    [StringLength(64)]
    [Display(Name = "Référence / SKU")]
    public string? Sku { get; set; }

    [Display(Name = "Catégorie")]
    public Guid? CategoryId { get; set; }

    [Display(Name = "Statut")]
    public ProductStatus Status { get; set; } = ProductStatus.Active;

    [Range(0, 1000000)]
    [Display(Name = "Quantité en stock")]
    public int StockQuantity { get; set; }

    [Range(0, 10000)]
    [Display(Name = "Poids (kg, facultatif)")]
    public decimal? WeightKg { get; set; }
}

public class CategoryFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Le nom est requis.")]
    [StringLength(120)]
    [Display(Name = "Nom de la catégorie")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Ordre d'affichage")]
    [Range(0, 9999)]
    public int DisplayOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public class OrderListItemViewModel
{
    public List<Order> Orders { get; set; } = new();
    public Dictionary<Guid, int> ItemCounts { get; set; } = new();
    public string? StatusFilter { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
}

public class OrderDetailsViewModel
{
    public Order Order { get; set; } = null!;
    public List<OrderStatusHistory> History { get; set; } = new();
    public string? WhatsAppUrl { get; set; }
    public OrderStatus[] AllowedNextStatuses { get; set; } = Array.Empty<OrderStatus>();
}

public class ProfileSettingsViewModel
{
    [Required(ErrorMessage = "Le nom commercial est requis.")]
    [StringLength(120)]
    [Display(Name = "Nom commercial")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(80)]
    public string? BusinessCategory { get; set; }

    [StringLength(2)]
    public string Country { get; set; } = "CD";

    public string? City { get; set; }
    public string? Address { get; set; }

    [Display(Name = "Numéro WhatsApp")]
    public string? WhatsappNumber { get; set; }

    [EmailAddress]
    public string? BusinessEmail { get; set; }

    [Display(Name = "Mode de décrément du stock")]
    public StockDecrementMode StockMode { get; set; } = StockDecrementMode.OnOrder;

    [Display(Name = "Mode de commande WhatsApp")]
    public WhatsAppOrderMode WhatsAppMode { get; set; } = WhatsAppOrderMode.RecordAndContact;
}

public class AppearanceSettingsViewModel
{
    public string ThemeKey { get; set; } = "generaliste";

    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Couleur hexadécimale attendue, ex. #0F4C3A")]
    public string PrimaryColor { get; set; } = "#0F4C3A";

    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Couleur hexadécimale attendue, ex. #E3A008")]
    public string SecondaryColor { get; set; } = "#E3A008";

    public string FontFamily { get; set; } = "system";
    public string? BannerText { get; set; }
    public bool ShowBanner { get; set; } = true;
}

public class SeoSettingsViewModel
{
    public string Currency { get; set; } = "USD";
    public string Timezone { get; set; } = "Africa/Kinshasa";

    [StringLength(120)]
    public string? SeoTitle { get; set; }

    [StringLength(300)]
    public string? SeoDescription { get; set; }

    public string? SocialInstagram { get; set; }
    public string? SocialFacebook { get; set; }
    public string? SocialTiktok { get; set; }
    public string? SocialYoutube { get; set; }

    [StringLength(400)]
    public string? DeliveryInfo { get; set; }

    public string? ContactPhone { get; set; }

    [Display(Name = "Livraison gratuite à partir de (montant)")]
    [Range(0, 1000000)]
    public decimal? FreeShippingOver { get; set; }

    [Display(Name = "Seuil d'alerte de stock faible")]
    [Range(0, 10000)]
    public int LowStockThreshold { get; set; } = 5;
}

namespace Adhicom.Web.ViewModels
{
    public class CategoryListItemViewModel
    {
        public Adhicom.Domain.Catalog.Category Category { get; set; } = new();
        public int ProductCount { get; set; }
    }
}
