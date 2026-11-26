using Adhicom.Application.Common;
using Adhicom.Domain.Administration;
using Adhicom.Domain.Catalog;
using Adhicom.Domain.Notifications;
using Adhicom.Domain.Orders;
using Adhicom.Domain.Payments;
using Adhicom.Domain.Shipping;
using Adhicom.Domain.Stores;
using Adhicom.Domain.Subscriptions;
using Adhicom.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Infrastructure.Persistence;

/// <summary>
/// DbContext principal (PostgreSQL). Multi-tenant : chaque entité tenant porte StoreId et est protégée
/// par un filtre global de requête.
/// IMPORTANT : le filtre se base sur des PROPRIÉTÉS du contexte (jamais sur des variables capturées),
/// afin d'être évalué à chaque requête. Le tenant est déterminé par le serveur à partir de l'utilisateur
/// authentifié / du sous-domaine — jamais à partir d'un paramètre client.
/// </summary>
public class AdhicomDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    private readonly ITenantContext _tenant;

    public AdhicomDbContext(DbContextOptions<AdhicomDbContext> options, ITenantContext tenant) : base(options)
        => _tenant = tenant;

    /// <summary>Boutique courante pour le filtre (Guid.Empty = aucune boutique : aucune ligne tenant visible, sauf bypass).</summary>
    public Guid CurrentStoreId => _tenant.StoreId ?? Guid.Empty;
    public bool BypassTenantFilter => _tenant.Bypass;

    // Tenants (boutiques)
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<StoreSettings> StoreSettings => Set<StoreSettings>();
    public DbSet<StoreDomain> StoreDomains => Set<StoreDomain>();
    public DbSet<StoreMember> StoreMembers => Set<StoreMember>();

    // Catalogue
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    // Commandes
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<ShoppingCart> ShoppingCarts => Set<ShoppingCart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    // Paiements
    public DbSet<PaymentProvider> PaymentProviders => Set<PaymentProvider>();
    public DbSet<StorePaymentConfiguration> StorePaymentConfigurations => Set<StorePaymentConfiguration>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();

    // Abonnements
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();
    public DbSet<SubscriptionHistory> SubscriptionHistories => Set<SubscriptionHistory>();

    // Livraison
    public DbSet<ShippingZone> ShippingZones => Set<ShippingZone>();
    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

    // Administration
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<SiteContent> SiteContents => Set<SiteContent>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<Report> Reports => Set<Report>();

    private void TenantFilter<TEntity>(ModelBuilder mb) where TEntity : class
        => mb.Entity<TEntity>().HasQueryFilter(e => BypassTenantFilter || EF.Property<Guid>(e, "StoreId") == CurrentStoreId);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Noms de tables Identity alignés sur le cahier des charges
        modelBuilder.Entity<ApplicationUser>().ToTable("Users");
        modelBuilder.Entity<IdentityRole>().ToTable("Roles");
        modelBuilder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
        modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");
        modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");

        // ===== Boutiques =====
        modelBuilder.Entity<Store>(e =>
        {
            e.ToTable("Stores");
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(48).IsRequired();
            e.Property(x => x.PrimaryColor).HasMaxLength(16);
            e.Property(x => x.SecondaryColor).HasMaxLength(16);
            e.Property(x => x.ThemeKey).HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.StockMode).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.WhatsAppMode).HasConversion<string>().HasMaxLength(32);
        });
        modelBuilder.Entity<StoreSettings>(e =>
        {
            e.ToTable("StoreSettings");
            e.HasIndex(x => x.StoreId).IsUnique();
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.FreeShippingOver).HasPrecision(18, 2);
            e.HasOne(x => x.Store).WithOne(s => s!.Settings).HasForeignKey<StoreSettings>(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<StoreDomain>(e =>
        {
            e.ToTable("StoreDomains");
            e.HasIndex(x => x.Domain).IsUnique();
            e.Property(x => x.Domain).HasMaxLength(253).IsRequired();
            e.HasOne(x => x.Store).WithMany(s => s.Domains).HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<StoreMember>(e =>
        {
            e.ToTable("StoreMembers");
            e.HasIndex(x => new { x.StoreId, x.UserId }).IsUnique();
            e.Property(x => x.Role).HasMaxLength(32);
        });

        // ===== Catalogue =====
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasIndex(x => new { x.StoreId, x.Slug }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(64).IsRequired();
        });
        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.HasIndex(x => new { x.StoreId, x.Slug }).IsUnique();
            e.HasIndex(x => new { x.StoreId, x.Status });
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.CompareAtPrice).HasPrecision(18, 2);
            e.Property(x => x.WeightKg).HasPrecision(10, 3);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<ProductImage>(e =>
        {
            e.ToTable("ProductImages");
            e.Property(x => x.Url).HasMaxLength(1024).IsRequired();
            e.HasOne(x => x.Product).WithMany(p => p.Images).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ProductVariant>(e =>
        {
            e.ToTable("ProductVariants");
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.Property(x => x.Value).HasMaxLength(64).IsRequired();
            e.Property(x => x.PriceAdjustment).HasPrecision(18, 2);
            e.HasOne(x => x.Product).WithMany(p => p.Variants).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<InventoryMovement>(e =>
        {
            e.ToTable("InventoryMovements");
            e.Property(x => x.Reason).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Reference).HasMaxLength(64);
            e.HasIndex(x => new { x.StoreId, x.ProductId });
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        // ===== Commandes =====
        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("Customers");
            e.HasIndex(x => new { x.StoreId, x.Phone });
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(32).IsRequired();
            e.Property(x => x.TotalSpent).HasPrecision(18, 2);
        });
        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.HasIndex(x => new { x.StoreId, x.OrderNumber }).IsUnique();
            e.HasIndex(x => new { x.StoreId, x.Status });
            e.HasIndex(x => new { x.StoreId, x.CreatedAtUtc });
            e.Property(x => x.OrderNumber).HasMaxLength(32).IsRequired();
            e.Property(x => x.CustomerName).HasMaxLength(160).IsRequired();
            e.Property(x => x.CustomerPhone).HasMaxLength(32).IsRequired();
            e.Property(x => x.SubTotal).HasPrecision(18, 2);
            e.Property(x => x.DiscountTotal).HasPrecision(18, 2);
            e.Property(x => x.ShippingFee).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<OrderItem>(e =>
        {
            e.ToTable("OrderItems");
            e.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            e.Property(x => x.VariantName).HasMaxLength(128);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.HasOne(x => x.Order).WithMany(o => o.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.OrderId);
        });
        modelBuilder.Entity<OrderStatusHistory>(e =>
        {
            e.ToTable("OrderStatusHistory");
            e.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Comment).HasMaxLength(500);
            e.HasOne(x => x.Order).WithMany(o => o.StatusHistory).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ShoppingCart>(e =>
        {
            e.ToTable("ShoppingCarts");
            e.HasIndex(x => new { x.StoreId, x.Token }).IsUnique();
            e.Property(x => x.Token).HasMaxLength(64).IsRequired();
        });
        modelBuilder.Entity<CartItem>(e =>
        {
            e.ToTable("CartItems");
            e.HasOne(x => x.Cart).WithMany(c => c.Items).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
        });

        // ===== Paiements =====
        modelBuilder.Entity<PaymentProvider>(e =>
        {
            e.ToTable("PaymentProviders");
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(32).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        });
        modelBuilder.Entity<StorePaymentConfiguration>(e =>
        {
            e.ToTable("StorePaymentConfigurations");
            e.HasIndex(x => new { x.StoreId, x.Method }).IsUnique();
            e.Property(x => x.Method).HasMaxLength(32).IsRequired();
        });
        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("Payments");
            e.Property(x => x.Method).HasMaxLength(32).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(32).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.TransactionReference).HasMaxLength(128);
            e.HasIndex(x => new { x.StoreId, x.OrderId });
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PaymentTransaction>(e =>
        {
            e.ToTable("PaymentTransactions");
            e.Property(x => x.Type).HasMaxLength(32).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PaymentWebhookEvent>(e =>
        {
            e.ToTable("PaymentWebhookEvents");
            e.HasIndex(x => new { x.Provider, x.EventId }).IsUnique();
            e.Property(x => x.Provider).HasMaxLength(32).IsRequired();
            e.Property(x => x.EventId).HasMaxLength(128).IsRequired();
        });

        // ===== Abonnements =====
        modelBuilder.Entity<Plan>(e =>
        {
            e.ToTable("Plans");
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(32).IsRequired();
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.Property(x => x.MonthlyPrice).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
        });
        modelBuilder.Entity<PlanFeature>(e =>
        {
            e.ToTable("PlanFeatures");
            e.Property(x => x.Label).HasMaxLength(160).IsRequired();
            e.HasOne(x => x.Plan).WithMany(p => p.Features).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Subscription>(e =>
        {
            e.ToTable("Subscriptions");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => x.SubscriberUserId);
            e.HasIndex(x => new { x.Status, x.EndsAtUtc });
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SubscriptionPayment>(e =>
        {
            e.ToTable("SubscriptionPayments");
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Method).HasMaxLength(48).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SubscriptionHistory>(e =>
        {
            e.ToTable("SubscriptionHistory");
            e.Property(x => x.Event).HasMaxLength(48).IsRequired();
            e.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });

        // ===== Livraison =====
        modelBuilder.Entity<ShippingZone>(e =>
        {
            e.ToTable("ShippingZones");
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        });
        modelBuilder.Entity<ShippingMethod>(e =>
        {
            e.ToTable("ShippingMethods");
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.FreeOverAmount).HasPrecision(18, 2);
            e.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.SetNull);
        });

        // ===== Notifications =====
        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("Notifications");
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Body).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Type).HasMaxLength(32);
            e.HasIndex(x => new { x.UserId, x.IsRead });
        });
        modelBuilder.Entity<NotificationTemplate>(e =>
        {
            e.ToTable("NotificationTemplates");
            e.HasIndex(x => new { x.Key, x.Channel }).IsUnique();
            e.Property(x => x.Key).HasMaxLength(64).IsRequired();
            e.Property(x => x.Channel).HasMaxLength(16).IsRequired();
        });

        // ===== Administration =====
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.Property(x => x.Action).HasMaxLength(96).IsRequired();
            e.Property(x => x.Entity).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.CreatedAtUtc);
        });
        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.ToTable("SystemSettings");
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(128).IsRequired();
        });
        modelBuilder.Entity<SiteContent>(e =>
        {
            e.ToTable("SiteContents");
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(128).IsRequired();
        });
        modelBuilder.Entity<SupportTicket>(e =>
        {
            e.ToTable("SupportTickets");
            e.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        });
        modelBuilder.Entity<Report>(e =>
        {
            e.ToTable("Reports");
            e.Property(x => x.Reason).HasMaxLength(160).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        });

        // ===== Filtres multi-tenant (isolation stricte) =====
        TenantFilter<StoreSettings>(modelBuilder);
        TenantFilter<StoreDomain>(modelBuilder);
        TenantFilter<StoreMember>(modelBuilder);
        TenantFilter<Category>(modelBuilder);
        TenantFilter<Product>(modelBuilder);
        TenantFilter<ProductImage>(modelBuilder);
        TenantFilter<ProductVariant>(modelBuilder);
        TenantFilter<InventoryMovement>(modelBuilder);
        TenantFilter<Customer>(modelBuilder);
        TenantFilter<Order>(modelBuilder);
        TenantFilter<OrderItem>(modelBuilder);
        TenantFilter<OrderStatusHistory>(modelBuilder);
        TenantFilter<ShoppingCart>(modelBuilder);
        TenantFilter<CartItem>(modelBuilder);
        TenantFilter<StorePaymentConfiguration>(modelBuilder);
        TenantFilter<Payment>(modelBuilder);
        TenantFilter<PaymentTransaction>(modelBuilder);
        TenantFilter<ShippingZone>(modelBuilder);
        TenantFilter<ShippingMethod>(modelBuilder);
    }
}
