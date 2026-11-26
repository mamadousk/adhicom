using Adhicom.Domain.Common;

namespace Adhicom.Domain.Subscriptions;

/// <summary>Plan tarifaire configurable depuis l'administration (jamais codé en dur).</summary>
public class Plan : AuditableEntity
{
    public required string Key { get; set; }           // free, starter, pro, business
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = Currencies.Usd;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Limites configurables (fonctionnalités contrôlées côté serveur)
    public int MaxStores { get; set; } = 1;
    public int MaxProducts { get; set; } = 20;
    public int MaxUsers { get; set; } = 1;
    public int MaxStorageMb { get; set; } = 100;
    public bool AllowPremiumThemes { get; set; }
    public bool AllowAdvancedAnalytics { get; set; }
    public bool AllowCustomDomain { get; set; }        // Préparé pour une version ultérieure
    public bool AllowMarketing { get; set; }
    public bool AllowCoupons { get; set; }
    public bool AllowExports { get; set; }

    public ICollection<PlanFeature> Features { get; set; } = new List<PlanFeature>();
}

/// <summary>Fonctionnalité/limité marketing affichée sur la page tarifs.</summary>
public class PlanFeature : Entity
{
    public Guid PlanId { get; set; }
    public Plan? Plan { get; set; }
    public required string Label { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Abonnement d'un commerçant (propriétaire) à un plan.</summary>
public class Subscription : AuditableEntity
{
    public Guid SubscriberUserId { get; set; }         // Utilisateur propriétaire du compte
    public Guid PlanId { get; set; }
    public Plan? Plan { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime StartsAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime EndsAtUtc { get; set; }
    public DateTime? GraceEndsAtUtc { get; set; }
    public bool AutoRenew { get; set; } = true;
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
}

public class SubscriptionPayment : AuditableEntity
{
    public Guid SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Currencies.Usd;
    public required string Method { get; set; }        // Mobile Money, virement, carte...
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? Reference { get; set; }
    public DateTime? PaidAtUtc { get; set; }
}

public class SubscriptionHistory : AuditableEntity
{
    public Guid SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    public required string Event { get; set; }         // created, renewed, plan_changed, grace_started, expired, cancelled, reactivated
    public string? Detail { get; set; }
    public Guid? ChangedByUserId { get; set; }
}
