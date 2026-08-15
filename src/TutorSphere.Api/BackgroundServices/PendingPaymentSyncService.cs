using Microsoft.EntityFrameworkCore;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Api.BackgroundServices;

/// <summary>
/// Active les forfaits payés même si le parent ferme l'onglet avant le poll <c>confirm</c>.
/// </summary>
public class PendingPaymentSyncService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PendingPaymentSyncService> _logger;

    public PendingPaymentSyncService(
        IServiceProvider services,
        ILogger<PendingPaymentSyncService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var payments = scope.ServiceProvider.GetRequiredService<IPaymentGatewayService>();
                var n = await payments.SyncPendingPaymentsAsync(stoppingToken);
                if (n > 0)
                    _logger.LogInformation("Synchronisation {Count} paiement(s) en attente", n);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Erreur de synchronisation des paiements en attente.");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }
}

/// <summary>
/// Expire les forfaits (date dépassée, ou plus de séances et plus de cours futurs).
/// </summary>
public class SubscriptionPackExpiryService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SubscriptionPackExpiryService> _logger;

    public SubscriptionPackExpiryService(
        IServiceProvider services,
        ILogger<SubscriptionPackExpiryService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Erreur lors de l'expiration des forfaits.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task ExpireAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TutorSphere.Infrastructure.Persistence.ApplicationDbContext>();
        var scheduler = scope.ServiceProvider
            .GetRequiredService<TutorSphere.Application.Services.ISubscriptionLessonScheduler>();

        var now = DateTime.UtcNow;
        var active = await db.StudentSubscriptionsSet
            .Where(s => s.Status == SubscriptionStatus.Active)
            .ToListAsync(ct);

        var expired = 0;
        foreach (var sub in active)
        {
            var pastEnd = sub.EndDate < now;
            if (!pastEnd && sub.SessionsRemaining > 0)
                continue;

            var marker = TutorSphere.Application.Services.SubscriptionLessonScheduler.MarkerFor(sub.Id);
            var hasFuture = await db.LessonsSet.IgnoreQueryFilters()
                .AnyAsync(l => l.TenantId == sub.TenantId
                               && l.SessionNotes != null
                               && l.SessionNotes.Contains(marker)
                               && l.StartTime > now
                               && l.SettlementStatus == LessonSettlementStatus.Scheduled
                               && !l.SessionCounted, ct);

            if (!pastEnd && hasFuture)
                continue;

            sub.Status = SubscriptionStatus.Expired;
            sub.UpdatedAt = now;
            expired++;
            await scheduler.CancelUnconsumedFutureAsync(sub.Id, ct);
        }

        if (expired > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Forfaits expirés : {Count}", expired);
        }
    }
}
