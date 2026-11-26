using System.Globalization;
using Adhicom.Domain.Common;
using Adhicom.Domain.Administration;

namespace Adhicom.Web.Infrastructure;

/// <summary>Aides de présentation (vues) : devises et libellés du domaine en français.</summary>
public static class Ui
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static string Money(decimal amount, string? currency = "USD")
    {
        var symbol = currency switch
        {
            "USD" => "$",
            "CDF" => "FC",
            "XAF" or "XOF" => "FCFA",
            _ => currency ?? "USD"
        };
        return $"{amount.ToString("N2", Fr)} {symbol}";
    }

    public static string StoreStatus(StoreStatus s) => s switch
    {
        StoreStatus.Draft => "Brouillon",
        StoreStatus.PendingValidation => "En attente de validation",
        StoreStatus.Active => "Active",
        StoreStatus.Suspended => "Suspendue",
        StoreStatus.Disabled => "Désactivée",
        StoreStatus.Closed => "Fermée",
        _ => s.ToString()
    };

    public static string ProductStatus(ProductStatus s) => s switch
    {
        ProductStatus.Draft => "Brouillon",
        ProductStatus.Active => "Publié",
        ProductStatus.Archived => "Archivé",
        _ => s.ToString()
    };

    public static string OrderStatus(OrderStatus s) => s switch
    {
        OrderStatus.Pending => "En attente",
        OrderStatus.Confirmed => "Confirmée",
        OrderStatus.Preparing => "En préparation",
        OrderStatus.ReadyToShip => "Prête à être expédiée",
        OrderStatus.OutForDelivery => "En livraison",
        OrderStatus.Delivered => "Livrée",
        OrderStatus.Cancelled => "Annulée",
        OrderStatus.Refused => "Refusée",
        OrderStatus.Returned => "Retournée",
        _ => s.ToString()
    };

    public static string PaymentStatus(PaymentStatus s) => s switch
    {
        PaymentStatus.Unpaid => "Non encaissé",
        PaymentStatus.Pending => "En attente",
        PaymentStatus.Paid => "Payée",
        PaymentStatus.Failed => "Échoué",
        PaymentStatus.Refunded => "Remboursé",
        PaymentStatus.ToBeConfirmed => "À confirmer",
        _ => s.ToString()
    };

    public static string PaymentMethod(string method) => method switch
    {
        PaymentMethods.CashOnDelivery => "Paiement à la livraison",
        PaymentMethods.Manual => "Paiement manuel",
        PaymentMethods.MobileMoney => "Mobile Money",
        PaymentMethods.Card => "Carte bancaire",
        PaymentMethods.BankTransfer => "Virement bancaire",
        _ => method
    };

    public static string SubscriptionStatus(SubscriptionStatus s) => s switch
    {
        SubscriptionStatus.Active => "Active",
        SubscriptionStatus.Grace => "Période de grâce",
        SubscriptionStatus.PastDue => "Impayée (limitée)",
        SubscriptionStatus.Cancelled => "Annulée",
        SubscriptionStatus.Expired => "Expirée",
        _ => s.ToString()
    };

    public static string TicketStatus(TicketStatus s) => s switch
    {
        TicketStatus.Open => "Ouvert",
        TicketStatus.InProgress => "En cours",
        TicketStatus.Resolved => "Résolu",
        TicketStatus.Closed => "Fermé",
        _ => s.ToString()
    };

    public static string ReportStatus(ReportStatus s) => s switch
    {
        ReportStatus.New => "Nouveau",
        ReportStatus.Reviewing => "En cours",
        ReportStatus.Actioned => "Traité",
        ReportStatus.Dismissed => "Classé sans suite",
        _ => s.ToString()
    };

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant()
            : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }
}
