using Adhicom.Domain.Common;

namespace Adhicom.Domain.Stores;

/// <summary>Une boutique Adhicom (tenant). Le champ Slug est le sous-domaine public.</summary>
public class Store : AuditableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }          // ex. "fashionhouse" -> fashionhouse.adhicom.com
    public string? Description { get; set; }
    public string? BusinessCategory { get; set; }      // ex. "Mode et vêtements"
    public string Country { get; set; } = "CD";
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? WhatsappNumber { get; set; }
    public string? BusinessEmail { get; set; }
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string ThemeKey { get; set; } = "generaliste"; // mode, sneakers, electronique, beaute, alimentation, generaliste
    public string PrimaryColor { get; set; } = "#0F4C3A";
    public string SecondaryColor { get; set; } = "#E3A008";
    public string FontFamily { get; set; } = "system";
    public StoreStatus Status { get; set; } = StoreStatus.Draft;
    public StockDecrementMode StockMode { get; set; } = StockDecrementMode.OnOrder;
    public WhatsAppOrderMode WhatsAppMode { get; set; } = WhatsAppOrderMode.RecordAndContact;
    public Guid OwnerId { get; set; }                  // Utilisateur propriétaire (Users)
    public string? SuspendedReason { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }

    public StoreSettings? Settings { get; set; }
    public ICollection<StoreMember> Members { get; set; } = new List<StoreMember>();
    public ICollection<StoreDomain> Domains { get; set; } = new List<StoreDomain>();
}

/// <summary>Paramètres étendus d'une boutique (1:1).</summary>
public class StoreSettings : AuditableEntity
{
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }

    public string Currency { get; set; } = Currencies.Usd;
    public string Timezone { get; set; } = "Africa/Kinshasa";
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? SocialInstagram { get; set; }
    public string? SocialFacebook { get; set; }
    public string? SocialTiktok { get; set; }
    public string? SocialYoutube { get; set; }
    public string? ContactPhone { get; set; }
    public string? DeliveryInfo { get; set; }          // Texte libre affiché sur la boutique
    public decimal? FreeShippingOver { get; set; }     // Livraison offerte au-delà de ce montant (null = jamais)
    public bool ShowBanner { get; set; } = true;
    public string? BannerText { get; set; }
    public int LowStockThreshold { get; set; } = 5;
}

/// <summary>Domaines rattachés à une boutique (sous-domaine automatique ou domaine personnalisé ultérieur).</summary>
public class StoreDomain : AuditableEntity
{
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }
    public required string Domain { get; set; }        // ex. "fashionhouse.adhicom.com" ou "www.maboutique.com"
    public bool IsPrimary { get; set; }
    public bool IsCustom { get; set; }                 // Domaine personnalisé (version ultérieure)
    public bool IsVerified { get; set; } = true;       // Sous-domaine auto = vérifié d'office ; custom = TXT/CNAME requis
    public DateTime? VerifiedAtUtc { get; set; }
}

/// <summary>Membres d'une boutique (le propriétaire est créé automatiquement ; multi-utilisateurs = version ultérieure).</summary>
public class StoreMember : AuditableEntity
{
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "owner";        // owner (extensible : manager, seller...)
}
