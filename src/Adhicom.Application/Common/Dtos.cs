namespace Adhicom.Application.Common;

public record CreateStoreRequest(
    Guid OwnerId,
    string Name,
    string? RequestedSlug,
    string? Description,
    string? BusinessCategory,
    string Country,
    string? City,
    string? Address,
    string? WhatsappNumber,
    string? BusinessEmail);

public record PlaceOrderCommand(
    Guid StoreId,
    string CartToken,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    string? CustomerCity,
    string? CustomerDistrict,
    string? CustomerAddress,
    string? CustomerInstructions,
    string PaymentMethod,
    Guid? ShippingMethodId = null);

public record PlaceOrderResult(Guid OrderId, string OrderNumber, string PaymentMethod, string? WhatsAppRedirectUrl);

public record StorefrontTotals(decimal SubTotal, decimal ShippingFee, decimal DiscountTotal, decimal Total);

/// <summary>Partage de produits sur WhatsApp (message prérempli).</summary>
public static class WhatsAppLink
{
    public static string Build(string? internationalNumber, string message)
    {
        var n = (internationalNumber ?? string.Empty).Trim().TrimStart('+').Replace(" ", "").Replace("-", "");
        return $"https://wa.me/{n}?text={Uri.EscapeDataString(message)}";
    }
}
