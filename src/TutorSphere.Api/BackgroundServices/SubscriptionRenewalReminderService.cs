using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Persistence;

namespace TutorSphere.Api.BackgroundServices;

/// <summary>
/// Rappelle les parents avant la fin du forfait (fenêtre proportionnelle à DurationDays).
/// </summary>
public class SubscriptionRenewalReminderService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SubscriptionRenewalReminderService> _logger;

    public SubscriptionRenewalReminderService(
        IServiceProvider services,
        ILogger<SubscriptionRenewalReminderService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Erreur lors des rappels de renouvellement d'abonnement.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var webBase = (configuration["WebBaseUrl"] ?? "https://tutorsphere.gisebs.com").TrimEnd('/');
        var payUrl = $"{webBase}/parent/subscriptions";

        var now = DateTime.UtcNow;
        var candidates = await db.StudentSubscriptionsSet
            .Where(s => s.Status == SubscriptionStatus.Active
                        && s.EndDate > now
                        && s.RenewalReminderSentAt == null)
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return;

        var sent = 0;
        foreach (var sub in candidates)
        {
            try
            {
                var offering = await db.SubscriptionOfferingsSet.AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == sub.OfferingId, ct);
                var windowDays = SubscriptionPackRules.RenewalWindowDays(
                    offering?.DurationDays > 0 ? offering.DurationDays : 30);
                if (sub.EndDate > now.AddDays(windowDays))
                    continue;

                var student = await db.StudentsSet.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == sub.StudentId, ct);
                if (student?.ParentProfileId is not Guid parentId)
                    continue;

                var parent = await db.ParentProfilesSet.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == parentId, ct);
                if (parent is null || string.IsNullOrWhiteSpace(parent.Email))
                    continue;

                await email.SendParentSubscriptionRenewalReminderAsync(
                    parent.Email,
                    parent.FirstName,
                    $"{student.FirstName} {student.LastName}".Trim(),
                    offering?.Title ?? "Cours",
                    sub.EndDate,
                    payUrl,
                    ct);

                sub.RenewalReminderSentAt = now;
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rappel renouvellement échoué pour abonnement {SubscriptionId}", sub.Id);
            }
        }

        if (sent > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Rappels renouvellement abonnement envoyés : {Count}", sent);
        }
    }
}
