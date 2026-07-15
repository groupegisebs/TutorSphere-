using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Persistence;

namespace TutorSphere.Api.BackgroundServices;

/// <summary>
/// Relance les parents dont l'abonnement est en AwaitingPayment depuis plus de 2 jours.
/// </summary>
public class OverduePaymentReminderService : BackgroundService
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(2);
    private static readonly TimeSpan ResendInterval = TimeSpan.FromDays(5);

    private readonly IServiceProvider _services;
    private readonly ILogger<OverduePaymentReminderService> _logger;

    public OverduePaymentReminderService(
        IServiceProvider services,
        ILogger<OverduePaymentReminderService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Délai initial pour laisser démarrer l'API
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendOverdueRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Erreur lors des rappels de paiement en retard.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task SendOverdueRemindersAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var webBase = (configuration["WebBaseUrl"] ?? "https://app.tutorsphere.gisebs.com").TrimEnd('/');
        var payUrl = $"{webBase}/parent/subscriptions";

        var cutoff = DateTime.UtcNow - GracePeriod;
        var now = DateTime.UtcNow;

        var overdue = await db.StudentSubscriptionsSet
            .Where(s => s.Status == SubscriptionStatus.AwaitingPayment
                        && s.UpdatedAt <= cutoff
                        && (s.OverdueReminderSentAt == null
                            || s.OverdueReminderSentAt <= now - ResendInterval))
            .ToListAsync(ct);

        if (overdue.Count == 0)
            return;

        var sent = 0;
        foreach (var sub in overdue)
        {
            try
            {
                var student = await db.StudentsSet.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == sub.StudentId, ct);
                if (student?.ParentProfileId is not Guid parentId)
                    continue;

                var parent = await db.ParentProfilesSet.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == parentId, ct);
                if (parent is null || string.IsNullOrWhiteSpace(parent.Email))
                    continue;

                var offering = await db.SubscriptionOfferingsSet.AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == sub.OfferingId, ct);

                await email.SendParentPaymentOverdueAsync(
                    parent.Email,
                    parent.FirstName,
                    $"{student.FirstName} {student.LastName}".Trim(),
                    offering?.Title ?? "Cours",
                    payUrl,
                    ct);

                sub.OverdueReminderSentAt = now;
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Échec rappel paiement en retard pour abonnement {SubscriptionId}", sub.Id);
            }
        }

        if (sent > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Rappels paiement en retard envoyés : {Count}", sent);
        }
    }
}
