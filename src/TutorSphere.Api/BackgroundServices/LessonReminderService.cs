using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Infrastructure.Identity;
using TutorSphere.Infrastructure.Persistence;

namespace TutorSphere.Api.BackgroundServices;

public class LessonReminderService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<LessonReminderService> _logger;

    public LessonReminderService(IServiceProvider services, ILogger<LessonReminderService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Erreur lors de l'envoi des rappels de cours.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var now = DateTime.UtcNow;
        var windowStart = now.AddHours(23);
        var windowEnd = now.AddHours(25);

        var lessons = await db.LessonsSet
            .Where(l => l.ReminderSentAt == null
                        && l.StartTime >= windowStart
                        && l.StartTime <= windowEnd)
            .Include(l => l.Attendances)
            .ThenInclude(a => a.Student)
            .ToListAsync(ct);

        foreach (var lesson in lessons)
        {
            var tenant = await db.TenantsSet.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == lesson.TenantId, ct);
            var tutorName = tenant?.Name ?? "Votre tuteur";
            var subject = lesson.Subject ?? lesson.Title;

            // Tutor / school owner
            if (tenant is not null && !string.IsNullOrWhiteSpace(tenant.OwnerUserId))
            {
                var tutor = await userManager.FindByIdAsync(tenant.OwnerUserId);
                if (tutor is not null
                    && tutor.EmailLessonReminders
                    && !string.IsNullOrWhiteSpace(tutor.Email))
                {
                    try
                    {
                        await email.SendLessonReminderAsync(
                            tutor.Email,
                            tutor.FullName,
                            tutorName,
                            subject,
                            lesson.StartTime,
                            ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Échec rappel tuteur pour le cours {LessonId}", lesson.Id);
                    }
                }
            }

            foreach (var attendance in lesson.Attendances)
            {
                var student = attendance.Student;
                if (student is null) continue;

                try
                {
                    if (!string.IsNullOrWhiteSpace(student.Email)
                        && await ShouldSendToUserEmailAsync(userManager, student.UserId, student.Email))
                    {
                        await email.SendLessonReminderAsync(
                            student.Email,
                            $"{student.FirstName} {student.LastName}".Trim(),
                            tutorName,
                            subject,
                            lesson.StartTime,
                            ct);
                    }

                    if (student.IsMinor && student.ParentProfileId is Guid parentId)
                    {
                        var parent = await db.ParentProfilesSet
                            .FirstOrDefaultAsync(p => p.Id == parentId, ct);
                        if (parent is not null
                            && !string.IsNullOrWhiteSpace(parent.Email)
                            && await ShouldSendToUserEmailAsync(userManager, parent.UserId, parent.Email))
                        {
                            await email.SendLessonReminderAsync(
                                parent.Email,
                                parent.FirstName,
                                tutorName,
                                subject,
                                lesson.StartTime,
                                ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Échec d'envoi du rappel pour le cours {LessonId}, étudiant {StudentId}",
                        lesson.Id, student.Id);
                }
            }

            lesson.ReminderSentAt = DateTime.UtcNow;
            lesson.UpdatedAt = DateTime.UtcNow;
        }

        if (lessons.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Prefer ApplicationUser.EmailLessonReminders when a linked account exists;
    /// otherwise allow reminders to the profile email (default on).
    /// </summary>
    private static async Task<bool> ShouldSendToUserEmailAsync(
        UserManager<ApplicationUser> userManager,
        string? userId,
        string email)
    {
        ApplicationUser? user = null;
        if (!string.IsNullOrWhiteSpace(userId))
            user = await userManager.FindByIdAsync(userId);

        user ??= await userManager.FindByEmailAsync(email);
        return user?.EmailLessonReminders ?? true;
    }
}
