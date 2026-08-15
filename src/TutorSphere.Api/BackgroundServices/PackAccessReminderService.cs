using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Persistence;

namespace TutorSphere.Api.BackgroundServices;

/// <summary>
/// Rappels : 1–2 séances restantes, et J-2 avant un cours si impayé ou pack non utilisable.
/// </summary>
public class PackAccessReminderService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PackAccessReminderService> _logger;

    public PackAccessReminderService(
        IServiceProvider services,
        ILogger<PackAccessReminderService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Erreur lors des rappels d'accès / séances restantes.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task SendAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var access = scope.ServiceProvider.GetRequiredService<ILessonAccessService>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var webBase = (configuration["WebBaseUrl"] ?? "https://app.tutorsphere.gisebs.com").TrimEnd('/');
        var payUrl = $"{webBase}/parent/subscriptions";
        var now = DateTime.UtcNow;
        var sent = 0;

        sent += await SendLowSessionsAsync(db, email, payUrl, now, ct);
        sent += await SendLessonAccessJ2Async(db, email, access, payUrl, now, ct);

        if (sent > 0)
            _logger.LogInformation("Rappels accès forfait envoyés : {Count}", sent);
    }

    private static async Task<int> SendLowSessionsAsync(
        ApplicationDbContext db,
        IEmailService email,
        string payUrl,
        DateTime now,
        CancellationToken ct)
    {
        var due = await db.StudentSubscriptionsSet
            .Where(s => s.Status == SubscriptionStatus.Active
                        && s.SessionsRemaining > 0
                        && s.SessionsRemaining <= 2
                        && s.LowSessionsReminderSentAt == null
                        && s.EndDate > now)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var sub in due)
        {
            if (!await TryNotifyParentAsync(db, email, sub, payUrl, ct, useRenewal: true))
                continue;

            sub.LowSessionsReminderSentAt = now;
            sent++;
        }

        if (sent > 0)
            await db.SaveChangesAsync(ct);
        return sent;
    }

    private static async Task<int> SendLessonAccessJ2Async(
        ApplicationDbContext db,
        IEmailService email,
        ILessonAccessService access,
        string payUrl,
        DateTime now,
        CancellationToken ct)
    {
        var windowStart = now.AddHours(42);
        var windowEnd = now.AddHours(54);

        var lessons = await db.LessonsSet
            .Where(l => l.StartTime >= windowStart
                        && l.StartTime <= windowEnd
                        && l.SettlementStatus == LessonSettlementStatus.Scheduled)
            .Include(l => l.Attendances)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var lesson in lessons)
        {
            foreach (var attendance in lesson.Attendances)
            {
                if (access.CanAttendLesson(attendance.StudentId, lesson.Id))
                    continue;

                var target = await db.StudentSubscriptionsSet
                    .Where(s => s.StudentId == attendance.StudentId && s.TenantId == lesson.TenantId)
                    .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                    .FirstOrDefaultAsync(ct);
                if (target is null || target.LessonAccessReminderSentAt is not null)
                    continue;

                if (!await TryNotifyParentAsync(db, email, target, payUrl, ct, useRenewal: false))
                    continue;

                target.LessonAccessReminderSentAt = now;
                sent++;
            }
        }

        if (sent > 0)
            await db.SaveChangesAsync(ct);
        return sent;
    }

    private static async Task<bool> TryNotifyParentAsync(
        ApplicationDbContext db,
        IEmailService email,
        TutorSphere.Domain.Entities.StudentSubscription sub,
        string payUrl,
        CancellationToken ct,
        bool useRenewal)
    {
        var student = await db.StudentsSet.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sub.StudentId, ct);
        if (student?.ParentProfileId is not Guid parentId)
            return false;

        var parent = await db.ParentProfilesSet.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == parentId, ct);
        if (parent is null || string.IsNullOrWhiteSpace(parent.Email))
            return false;

        var offering = await db.SubscriptionOfferingsSet.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == sub.OfferingId, ct);
        var studentName = $"{student.FirstName} {student.LastName}".Trim();
        var title = offering?.Title ?? "Cours";

        if (useRenewal)
        {
            await email.SendParentSubscriptionRenewalReminderAsync(
                parent.Email, parent.FirstName, studentName, title, sub.EndDate, payUrl, ct);
        }
        else
        {
            await email.SendParentPaymentOverdueAsync(
                parent.Email, parent.FirstName, studentName, title, payUrl, ct);
        }

        return true;
    }
}
