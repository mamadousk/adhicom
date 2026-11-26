using Adhicom.Application.Common;
using Adhicom.Domain.Orders;
using Adhicom.Domain.Common;

namespace Adhicom.Application.Orders;

public interface ICartService
{
    Task<ShoppingCart> GetOrCreateAsync(Guid storeId, string token, CancellationToken ct = default);
    Task<OperationResult> AddItemAsync(Guid storeId, string token, Guid productId, Guid? variantId, int quantity, CancellationToken ct = default);
    Task SetQuantityAsync(Guid storeId, string token, Guid cartItemId, int quantity, CancellationToken ct = default);
    Task RemoveItemAsync(Guid storeId, string token, Guid cartItemId, CancellationToken ct = default);
    Task ClearAsync(Guid storeId, string token, CancellationToken ct = default);
    Task<(decimal SubTotal, decimal ShippingFee, decimal DiscountTotal, decimal Total)> ComputeTotalsAsync(Guid storeId, ShoppingCart cart, Guid? shippingMethodId = null, CancellationToken ct = default);
}

public interface IOrderService
{
    /// <summary>Place la commande : recalcul serveur des montants, client, instantanés, historique, décrément stock selon le réglage. Idempotent sur panier vidé.</summary>
    Task<OperationResult<PlaceOrderResult>> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken ct = default);

    /// <summary>Transition de statut contrôlée (ancien -> nouveau) avec historisation et effets (décrément/restock).</summary>
    Task<OperationResult> UpdateStatusAsync(Guid orderId, OrderStatus newStatus, string? comment, Guid? changedByUserId, string? changedByName, CancellationToken ct = default);

    /// <summary>Confirme un paiement manuel (validation par le commerçant).</summary>
    Task<OperationResult> ConfirmManualPaymentAsync(Guid orderId, Guid confirmedByUserId, string? reference, CancellationToken ct = default);
}

public interface ICustomerService
{
    Task<Customer> FindOrCreateAsync(Guid storeId, string name, string phone, string? email, string? address, string? city, CancellationToken ct = default);
}
