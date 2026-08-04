using TutorSphere.Application.Services;

namespace TutorSphere.Api.BackgroundServices;

/// <summary>Passe les licences annuelles expirées en AwaitingRenewal.</summary>
public class PlatformLicenseExpiryService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PlatformLicenseExpiryService> _logger;

    public PlatformLicenseExpiryService(
        IServiceProvider services,
        ILogger<PlatformLicenseExpiryService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var billing = scope.ServiceProvider.GetRequiredService<IPlatformBillingService>();
                await billing.ExpireOverdueLicensesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Échec du balayage des licences plateforme expirées.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
