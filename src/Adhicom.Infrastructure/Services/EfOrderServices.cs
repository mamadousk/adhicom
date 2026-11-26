using Adhicom.Application.Catalog;
using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Orders;
using Adhicom.Domain.Common;
using Adhicom.Domain.Orders;
using Adhicom.Domain.Payments;
using Adhicom.Domain.Shipping;
using Adhicom.Domain.Stores;
using Adhicom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Infrastructure.Services;

public sealed class EfCustomerService : ICustomerService
{
    private readonly AdhicomDbContext _db;

    public EfCustomerService(AdhicomDbContext db) => _db = db;

    public async Task<Customer> FindOrCreateAsync(Guid storeId, string name, string phone, string? email, string? address, string? city, CancellationToken ct = default)
    {
        var normalizedPhone = (phone ?? string.Empty).Trim();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.StoreId == storeId && c.Phone == normalizedPhone, ct);
        if (customer is null)
        {
            customer = new Customer { StoreId = storeId, Name = name.Trim(), Phone = normalizedPhone, Email = email?.Trim(), Address = address?.Trim(), City = city?.Trim() };
            _db.Customers.Add(customer);
        }
        else
        {
            customer.Name = name.Trim();
            if (!string.IsNullOrWhiteSpace(email)) customer.Email = email.Trim();
            if (!string.IsNullOrWhiteSpace(address)) customer.Address = address.Trim();
            if (!string.IsNullOrWhiteSpace(city)) customer.City = city.Trim();
            customer.UpdatedAtUtc = DateTime.UtcNow;
        }
        return customer;
    }
}

public sealed class EfCartService : ICartService
{
    private const int MaxLineQuantity = 99;

    private readonly AdhicomDbContext _db;
    private readonly IOrderNumberGenerator _orderNumbers;

    public EfCartService(AdhicomDbContext db, IOrderNumberGenerator orderNumbers) => (_db, _orderNumbers) = (db, orderNumbers);

    public async Task<ShoppingCart> GetOrCreateAsync(Guid storeId, string token, CancellationToken ct = default)
    {
        var cart = await _db.ShoppingCarts.Include(c => c.Items).FirstOrDefaultAsync(c => c.StoreId == storeId && c.Token == token, ct);
        if (cart is not null) return cart;
        cart = new ShoppingCart { StoreId = storeId, Token = token };
        _db.ShoppingCarts.Add(cart);
        await _db.SaveChangesAsync(ct);
        return cart;
    }

    public async Task<OperationResult> AddItemAsync(Guid storeId, string token, Guid productId, Guid? variantId, int quantity, CancellationToken ct = default)
    {
        quantity = Math.Clamp(quantity, 1, MaxLineQuantity);
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId && p.StoreId == storeId, ct);
        if (product is null || product.Status != ProductStatus.Active)
            return OperationResult.Fail("Ce produit n'est plus disponible.");
        if (product.StockQuantity <= 0)
            return OperationResult.Fail("Ce produit est en rupture de stock.");

        if (variantId is { } vid)
        {
            var variantOk = await _db.ProductVariants.AsNoTracking().AnyAsync(v => v.Id == vid && v.ProductId == productId && v.StoreId == storeId, ct);
            if (!variantOk) return OperationResult.Fail("Variante introuvable pour ce produit.");
        }

        var cart = await GetOrCreateAsync(storeId, token, ct);
        var line = cart.Items.FirstOrDefault(i => i.ProductId == productId && i.VariantId == variantId);
        if (line is null)
        {
            _db.CartItems.Add(new CartItem { StoreId = storeId, CartId = cart.Id, ProductId = productId, VariantId = variantId, Quantity = quantity });
        }
        else
        {
            line.Quantity = Math.Clamp(line.Quantity + quantity, 1, MaxLineQuantity);
        }
        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    public async Task SetQuantityAsync(Guid storeId, string token, Guid cartItemId, int quantity, CancellationToken ct = default)
    {
        var cart = await _db.ShoppingCarts.Include(c => c.Items).FirstOrDefaultAsync(c => c.StoreId == storeId && c.Token == token, ct);
        var line = cart?.Items.FirstOrDefault(i => i.Id == cartItemId);
        if (cart is null || line is null) return;
        if (quantity <= 0) { _db.CartItems.Remove(line); }
        else { line.Quantity = Math.Clamp(quantity, 1, MaxLineQuantity); }
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveItemAsync(Guid storeId, string token, Guid cartItemId, CancellationToken ct = default)
    {
        var line = await _db.CartItems.FirstOrDefaultAsync(i => i.Id == cartItemId && i.StoreId == storeId && i.Cart!.Token == token, ct);
        if (line is null) return;
        _db.CartItems.Remove(line);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClearAsync(Guid storeId, string token, CancellationToken ct = default)
    {
        var cart = await _db.ShoppingCarts.Include(c => c.Items).FirstOrDefaultAsync(c => c.StoreId == storeId && c.Token == token, ct);
        if (cart is null) return;
        _db.CartItems.RemoveRange(cart.Items);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(decimal SubTotal, decimal ShippingFee, decimal DiscountTotal, decimal Total)> ComputeTotalsAsync(Guid storeId, ShoppingCart cart, Guid? shippingMethodId = null, CancellationToken ct = default)
    {
        var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.AsNoTracking().Where(p => p.StoreId == storeId && productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var variants = await _db.ProductVariants.AsNoTracking().Where(v => v.StoreId == storeId).ToListAsync(ct);

        decimal subTotal = 0;
        foreach (var item in cart.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product)) continue;
            var unit = product.Price;
            if (item.VariantId is { } vid)
            {
                var variant = variants.FirstOrDefault(v => v.Id == vid && v.ProductId == product.Id);
                if (variant?.PriceAdjustment is { } adj) unit = adj;
            }
            subTotal += unit * item.Quantity;
        }

        var settings = await _db.StoreSettings.AsNoTracking().FirstOrDefaultAsync(s => s.StoreId == storeId, ct);
        var shippingFee = 0m;
        if (shippingMethodId is { } sid)
        {
            var method = await _db.ShippingMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == sid && m.StoreId == storeId && m.IsActive, ct);
            if (method is not null && method.Type != ShippingMethodType.Pickup)
            {
                shippingFee = method.FreeOverAmount is { } fo && subTotal >= fo ? 0m
                    : settings?.FreeShippingOver is { } sfo && subTotal >= sfo ? 0m
                    : method.Price;
            }
        }

        const decimal discount = 0m; // Codes de réduction : version ultérieure (cf. cahier, §14 / §30.2)
        var total = subTotal - discount + shippingFee;
        return (subTotal, shippingFee, discount, total);
    }
}

public sealed class EfOrderService : IOrderService
{
    private readonly AdhicomDbContext _db;
    private readonly ICartService _cart;
    private readonly ICustomerService _customers;
    private readonly IInventoryService _inventory;
    private readonly IOrderNumberGenerator _orderNumbers;
    private readonly INotificationService _notifications;
    private readonly IEmailSender _email;
    private readonly IClock _clock;

    public EfOrderService(AdhicomDbContext db, ICartService cart, ICustomerService customers, IInventoryService inventory,
        IOrderNumberGenerator orderNumbers, INotificationService notifications, IEmailSender email, IClock clock)
        => (_db, _cart, _customers, _inventory, _orderNumbers, _notifications, _email, _clock) =
           (db, cart, customers, inventory, orderNumbers, notifications, email, clock);

    public async Task<OperationResult<PlaceOrderResult>> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerName)) return OperationResult<PlaceOrderResult>.Fail("Votre nom est requis.");
        if (string.IsNullOrWhiteSpace(command.CustomerPhone)) return OperationResult<PlaceOrderResult>.Fail("Votre numéro de téléphone est requis.");
        if (string.IsNullOrWhiteSpace(command.PaymentMethod)) return OperationResult<PlaceOrderResult>.Fail("Choisissez un moyen de paiement.");

        var store = await _db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == command.StoreId, ct);
        if (store is null || store.Status != StoreStatus.Active)
            return OperationResult<PlaceOrderResult>.Fail("Cette boutique n'accepte pas de commandes pour le moment.");

        var paymentEnabled = await _db.StorePaymentConfigurations.AsNoTracking()
            .AnyAsync(p => p.StoreId == store.Id && p.Method == command.PaymentMethod && p.IsEnabled, ct);
        if (!paymentEnabled)
            return OperationResult<PlaceOrderResult>.Fail("Ce moyen de paiement n'est pas disponible dans cette boutique.");

        var cart = await _db.ShoppingCarts.Include(c => c.Items).FirstOrDefaultAsync(c => c.StoreId == store.Id && c.Token == command.CartToken, ct);
        if (cart is null || cart.Items.Count == 0)
            return OperationResult<PlaceOrderResult>.Fail("Votre panier est vide.");

        // Recalcul 100% serveur des montants (on ne fait jamais confiance au client)
        var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => p.StoreId == store.Id && productIds.Contains(p.Id)).ToListAsync(ct);
        var variants = await _db.ProductVariants.Where(v => v.StoreId == store.Id).ToListAsync(ct);
        var settings = await _db.StoreSettings.FirstOrDefaultAsync(s => s.StoreId == store.Id, ct);

        // Pré-validation : stock suffisant pour toutes les lignes AVANT toute écriture
        foreach (var group in cart.Items.GroupBy(i => i.ProductId))
        {
            var needed = group.Sum(i => i.Quantity);
            var product = products.FirstOrDefault(p => p.Id == group.Key);
            if (product is not null && product.StockQuantity < needed)
                return OperationResult<PlaceOrderResult>.Fail($"Stock insuffisant pour « {product.Name} » ({product.StockQuantity} disponible(s)).");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            decimal subTotal = 0;
            foreach (var item in cart.Items)
            {
                var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product is null || product.Status != ProductStatus.Active)
                    return OperationResult<PlaceOrderResult>.Fail($"Un produit de votre panier (« {product?.Name ?? "inconnu"} ») n'est plus disponible.");
                var unit = product.Price;
                if (item.VariantId is { } vid)
                {
                    var variant = variants.FirstOrDefault(v => v.Id == vid && v.ProductId == product.Id);
                    if (variant?.PriceAdjustment is { } adj) unit = adj;
                }
                subTotal += unit * item.Quantity;
            }

            // Livraison : méthode choisie par le client (validée côté serveur), sinon la moins chère des modes à forfait actifs
            var shippingMethods = await _db.ShippingMethods.AsNoTracking().Where(m => m.StoreId == store.Id && m.IsActive).ToListAsync(ct);
            ShippingMethod? chosen = null;
            if (command.ShippingMethodId is { } sid)
                chosen = shippingMethods.FirstOrDefault(m => m.Id == sid);
            chosen ??= shippingMethods.Where(m => m.Type == ShippingMethodType.Flat).OrderBy(m => m.Price).FirstOrDefault();

            var shippingFee = 0m;
            string? shippingName = null;
            if (chosen is not null)
            {
                shippingName = chosen.Type == ShippingMethodType.Negotiated ? $"{chosen.Name} (à négocier)" : chosen.Name;
                shippingFee = chosen.Type switch
                {
                    ShippingMethodType.Flat => settings?.FreeShippingOver is { } sfo && subTotal >= sfo ? 0m
                        : chosen.FreeOverAmount is { } fo && subTotal >= fo ? 0m
                        : chosen.Price,
                    _ => 0m // Free / Pickup / Negotiated : pas de frais facturés ici
                };
            }

            const decimal discount = 0m;
            var total = subTotal - discount + shippingFee;
            var currency = settings?.Currency ?? Currencies.Usd;

            var customer = await _customers.FindOrCreateAsync(store.Id, command.CustomerName, command.CustomerPhone, command.CustomerEmail, command.CustomerAddress, command.CustomerCity, ct);

            // Numéro de commande public, unique, avec nouvelles tentatives contrôlées
            string orderNumber; var attempts = 0;
            do { orderNumber = _orderNumbers.Generate(); attempts++; }
            while (await _db.Orders.AsNoTracking().AnyAsync(o => o.OrderNumber == orderNumber, ct) && attempts < 5);

            var order = new Order
            {
                StoreId = store.Id,
                OrderNumber = orderNumber,
                Status = OrderStatus.Pending,
                PaymentMethod = command.PaymentMethod,
                PaymentStatus = command.PaymentMethod == PaymentMethods.CashOnDelivery ? PaymentStatus.Unpaid : PaymentStatus.ToBeConfirmed,
                CustomerId = customer.Id,
                CustomerName = command.CustomerName.Trim(),
                CustomerPhone = command.CustomerPhone.Trim(),
                CustomerEmail = command.CustomerEmail?.Trim(),
                CustomerCity = command.CustomerCity?.Trim(),
                CustomerDistrict = command.CustomerDistrict?.Trim(),
                CustomerAddress = command.CustomerAddress?.Trim(),
                CustomerInstructions = command.CustomerInstructions?.Trim(),
                SubTotal = subTotal,
                DiscountTotal = discount,
                ShippingFee = shippingFee,
                Total = total,
                Currency = currency,
                Notes = shippingName is null ? null : $"Livraison : {shippingName}",
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync(ct); // pour obtenir l'Id

            var itemsToSnapshot = new List<OrderItem>();
            foreach (var item in cart.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);
                var unit = product.Price;
                string? variantName = null;
                if (item.VariantId is { } vid)
                {
                    var variant = variants.FirstOrDefault(v => v.Id == vid);
                    if (variant is not null)
                    {
                        if (variant.PriceAdjustment is { } adj) unit = adj;
                        variantName = $"{variant.Name}: {variant.Value}";
                    }
                }
                _db.OrderItems.Add(new OrderItem
                {
                    StoreId = store.Id,
                    OrderId = order.Id,
                    ProductId = product.Id,
                    VariantId = item.VariantId,
                    ProductName = product.Name,
                    VariantName = variantName,
                    Sku = product.Sku,
                    UnitPrice = unit,
                    Quantity = item.Quantity
                });
                itemsToSnapshot.Add(new OrderItem { ProductName = product.Name, Quantity = item.Quantity, UnitPrice = unit, VariantName = variantName });
            }

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                StoreId = store.Id,
                OrderId = order.Id,
                OldStatus = null,
                NewStatus = OrderStatus.Pending,
                ChangedByName = "Client",
                Comment = "Commande passée depuis la boutique publique.",
                ChangedAtUtc = _clock.UtcNow
            });

            // Paiement
            var paymentStatus = command.PaymentMethod == PaymentMethods.CashOnDelivery ? PaymentStatus.Unpaid : PaymentStatus.ToBeConfirmed;
            var providerKey = command.PaymentMethod is PaymentMethods.MobileMoney or PaymentMethods.Card ? command.PaymentMethod : command.PaymentMethod;
            _db.Payments.Add(new Payment
            {
                StoreId = store.Id,
                OrderId = order.Id,
                Method = command.PaymentMethod,
                Provider = providerKey,
                Amount = total,
                Currency = currency,
                Status = paymentStatus
            });

            // Stock : décrément immédiat selon le réglage boutique
            if (store.StockMode == StockDecrementMode.OnOrder)
            {
                foreach (var item in cart.Items)
                {
                    var ok = await _inventory.TryDeductAsync(store.Id, item.ProductId, item.VariantId, item.Quantity, order.OrderNumber, null, ct, InventoryMovementReason.OrderPlaced);
                    if (!ok)
                    {
                        var p = products.FirstOrDefault(x => x.Id == item.ProductId);
                        return OperationResult<PlaceOrderResult>.Fail($"Stock insuffisant pour « {p?.Name ?? "un produit"} ». Veuillez ajuster votre panier.");
                    }
                }
                order.StockDeducted = true;
            }

            customer.TotalOrders += 1;
            customer.TotalSpent += total;
            customer.LastOrderAtUtc = _clock.UtcNow;

            // Vider le panier
            _db.CartItems.RemoveRange(cart.Items);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // Notifier le commerçant (in-app + email journalisé)
            var ownerId = await _db.Stores.AsNoTracking().Where(s => s.Id == store.Id).Select(s => (Guid?)s.OwnerId).FirstOrDefaultAsync(ct);
            if (ownerId is { } oid)
            {
                await _notifications.NotifyAsync(oid, store.Id, "order", "Nouvelle commande 🛒",
                    $"La commande {order.OrderNumber} de {order.CustomerName} ({total:N2} {currency}) est en attente.", $"/app/orders/details/{order.Id}", ct);
            }
            if (!string.IsNullOrWhiteSpace(order.CustomerEmail))
            {
                var lines = string.Join("\n", itemsToSnapshot.Select(i => $"- {i.ProductName} x{i.Quantity}"));
                await _email.SendAsync(order.CustomerEmail!, $"Commande {order.OrderNumber} — {store.Name}",
                    $"Bonjour {order.CustomerName},\n\nVotre commande {order.OrderNumber} a bien été enregistrée auprès de {store.Name}.\n\n{lines}\n\nTotal : {total:N2} {currency}\nPaiement : {LabelFor(command.PaymentMethod)}\n\nMerci de votre confiance.", ct);
            }

            string? whatsAppRedirect = null;
            if (store.WhatsAppMode == WhatsAppOrderMode.Redirect && !string.IsNullOrWhiteSpace(store.WhatsappNumber))
            {
                var recap = BuildWhatsAppRecap(store, order, itemsToSnapshot);
                whatsAppRedirect = WhatsAppLink.Build(store.WhatsappNumber, recap);
            }

            return OperationResult<PlaceOrderResult>.Ok(new PlaceOrderResult(order.Id, order.OrderNumber, command.PaymentMethod, whatsAppRedirect));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static string LabelFor(string method) => method switch
    {
        PaymentMethods.CashOnDelivery => "Paiement à la livraison",
        PaymentMethods.Manual => "Paiement manuel (confirmé par la boutique)",
        PaymentMethods.MobileMoney => "Mobile Money",
        PaymentMethods.Card => "Carte bancaire",
        PaymentMethods.BankTransfer => "Virement bancaire",
        _ => method
    };

    private static string BuildWhatsAppRecap(Store store, Order order, List<OrderItem> items)
    {
        var lines = string.Join("%0A", items.Select(i => $"• {i.ProductName} x{i.Quantity}"));
        return $"Bonjour {store.Name} ! Nouvelle commande {order.OrderNumber}%0A{lines}%0ATotal : {order.Total:N2} {order.Currency}%0ANom : {order.CustomerName}%0ATéléphone : {order.CustomerPhone}%0AAdresse : {order.CustomerAddress} ({order.CustomerDistrict}, {order.CustomerCity})";
    }

    public async Task<OperationResult> UpdateStatusAsync(Guid orderId, OrderStatus newStatus, string? comment, Guid? changedByUserId, string? changedByName, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct); // filtre tenant
        if (order is null) return OperationResult.Fail("Commande introuvable.");

        var oldStatus = order.Status;
        if (oldStatus == newStatus) return OperationResult.Ok();
        if (!OrderStatusMachine.CanTransition(oldStatus, newStatus))
            return OperationResult.Fail($"Transition non autorisée : « {FrenchStatus(oldStatus)} » → « {FrenchStatus(newStatus)} ».");

        var store = await _db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == order.StoreId, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Décrément à la confirmation si le réglage boutique est "À la confirmation"
            if (newStatus == OrderStatus.Confirmed && !order.StockDeducted && store is not null && store.StockMode == StockDecrementMode.OnConfirmation)
            {
                foreach (var item in order.Items.Where(i => i.ProductId is not null))
                {
                    var ok = await _inventory.TryDeductAsync(order.StoreId, item.ProductId!.Value, item.VariantId, item.Quantity, order.OrderNumber, changedByUserId, ct, InventoryMovementReason.OrderConfirmed);
                    if (!ok) return OperationResult.Fail("Stock insuffisant pour confirmer cette commande. Vérifiez vos stocks.");
                }
                order.StockDeducted = true;
            }

            // Annulation : recréditer si le stock avait été déduit
            if (newStatus is OrderStatus.Cancelled or OrderStatus.Refused)
            {
                if (order.StockDeducted)
                {
                    foreach (var item in order.Items.Where(i => i.ProductId is not null))
                        await _inventory.RestockAsync(order.StoreId, item.ProductId!.Value, item.VariantId, item.Quantity, order.OrderNumber, changedByUserId, ct);
                    order.StockDeducted = false;
                }
            }

            order.Status = newStatus;
            order.UpdatedAtUtc = _clock.UtcNow;
            if (newStatus == OrderStatus.Delivered) order.DeliveredAtUtc = _clock.UtcNow;

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                StoreId = order.StoreId,
                OrderId = order.Id,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangedByUserId = changedByUserId,
                ChangedByName = changedByName,
                Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
                ChangedAtUtc = _clock.UtcNow
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            await _email.SendAsync(order.CustomerEmail!, $"Commande {order.OrderNumber} — nouveau statut",
                $"Bonjour {order.CustomerName},\n\nVotre commande {order.OrderNumber} est maintenant : {FrenchStatus(newStatus)}.\n\nMerci de votre confiance.", ct);
        }
        return OperationResult.Ok();
    }

    public async Task<OperationResult> ConfirmManualPaymentAsync(Guid orderId, Guid confirmedByUserId, string? reference, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct); // filtre tenant
        if (order is null) return OperationResult.Fail("Commande introuvable.");
        if (order.PaymentStatus == PaymentStatus.Paid) return OperationResult.Ok(); // idempotent

        order.PaymentStatus = PaymentStatus.Paid;
        order.UpdatedAtUtc = _clock.UtcNow;

        var payment = await _db.Payments.Where(p => p.OrderId == order.Id).OrderByDescending(p => p.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (payment is not null)
        {
            payment.Status = PaymentStatus.Paid;
            payment.PaidAtUtc = _clock.UtcNow;
            if (!string.IsNullOrWhiteSpace(reference)) payment.TransactionReference = reference.Trim();
            _db.PaymentTransactions.Add(new PaymentTransaction
            {
                StoreId = order.StoreId,
                PaymentId = payment.Id,
                Type = "manual_confirm",
                Status = PaymentStatus.Paid,
                Reference = reference?.Trim(),
                Detail = "Paiement confirmé manuellement par le commerçant.",
                OccurredAtUtc = _clock.UtcNow
            });
        }
        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    public static string FrenchStatus(OrderStatus status) => status switch
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
        _ => status.ToString()
    };
}
