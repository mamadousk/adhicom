using Adhicom.Application.Subscriptions;

namespace Adhicom.Web.Infrastructure;

/// <summary>Traite périodiquement les échéances d'abonnements (expiration -> grâce -> limitation). Aucune donnée supprimée.</summary>
public sealed class SubscriptionMaintenanceHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionMaintenanceHostedService> _logger;

    public SubscriptionMaintenanceHostedService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionMaintenanceHostedService> logger)
        => (_scopeFactory, _logger) = (scopeFactory, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); // laisser l'application démarrer
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var maintenance = scope.ServiceProvider.GetRequiredService<ISubscriptionMaintenanceService>();
                var processed = await maintenance.ProcessExpirationsAsync(stoppingToken);
                if (processed > 0) _logger.LogInformation("Abonnements traités : {Count}", processed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du traitement des échéances d'abonnements");
            }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
