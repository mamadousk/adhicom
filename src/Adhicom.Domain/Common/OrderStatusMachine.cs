namespace Adhicom.Domain.Common;

/// <summary>
/// Machine à états du cycle de vie d'une commande. Source de vérité unique,
/// utilisée par le service métier et par l'interface (statuts proposés).
/// Toute transition est historisée par l'appelant (OrderStatusHistory).
/// </summary>
public static class OrderStatusMachine
{
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> AllowedMap = new Dictionary<OrderStatus, OrderStatus[]>
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Confirmed, OrderStatus.Cancelled, OrderStatus.Refused },
        [OrderStatus.Confirmed] = new[] { OrderStatus.Preparing, OrderStatus.ReadyToShip, OrderStatus.Cancelled },
        [OrderStatus.Preparing] = new[] { OrderStatus.ReadyToShip, OrderStatus.Cancelled },
        [OrderStatus.ReadyToShip] = new[] { OrderStatus.OutForDelivery, OrderStatus.Delivered, OrderStatus.Cancelled },
        [OrderStatus.OutForDelivery] = new[] { OrderStatus.Delivered, OrderStatus.Returned },
        [OrderStatus.Delivered] = new[] { OrderStatus.Returned },
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>(),
        [OrderStatus.Refused] = Array.Empty<OrderStatus>(),
        [OrderStatus.Returned] = Array.Empty<OrderStatus>(),
    };

    /// <summary>Transitions autorisées depuis un statut donné (jamais null).</summary>
    public static OrderStatus[] Allowed(OrderStatus from)
        => AllowedMap.TryGetValue(from, out var list) ? list : Array.Empty<OrderStatus>();

    /// <summary>Vrai si la transition depuis → vers est autorisée par le cycle de vie métier.</summary>
    public static bool CanTransition(OrderStatus from, OrderStatus to)
        => AllowedMap.TryGetValue(from, out var list) && list.Contains(to);

    /// <summary>Statuts terminaux (aucune transition possible).</summary>
    public static bool IsTerminal(OrderStatus status)
        => Allowed(status).Length == 0;
}
