using Microsoft.EntityFrameworkCore;
using TutorSphere.Application.Common.Interfaces;
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
            await SendRemindersAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var windowStart = now.AddHours(23);
        var windowEnd = now.AddHours(25);

        var lessons = await db.LessonsSet
            .Where(l => l.StartTime >= windowStart && l.StartTime <= windowEnd)
            .Include(l => l.Attendances)
            .ThenInclude(a => a.Student)
            .ToListAsync(ct);

        foreach (var lesson in lessons)
        {
            var tutorName = db.TenantsSet.FirstOrDefault(t => t.Id == lesson.TenantId)?.Name ?? "Votre tuteur";
            var subject = lesson.Subject ?? lesson.Title;

            foreach (var attendance in lesson.Attendances)
            {
                var student = attendance.Student;
                if (student is null) continue;

                try
                {
                    if (!string.IsNullOrWhiteSpace(student.Email))
                        await email.SendLessonReminderAsync(
                            student.Email,
                            $"{student.FirstName} {student.LastName}".Trim(),
                            tutorName,
                            subject,
                            lesson.StartTime,
                            ct);

                    if (student.IsMinor)
                    {
                        var parent = db.ParentProfilesSet.FirstOrDefault(p => p.Id == student.ParentProfileId);
                        if (parent is not null && !string.IsNullOrWhiteSpace(parent.Email))
                            await email.SendLessonReminderAsync(
                                parent.Email,
                                parent.FirstName,
                                tutorName,
                                subject,
                                lesson.StartTime,
                                ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Échec d'envoi du rappel pour le cours {LessonId}, étudiant {StudentId}", lesson.Id, student.Id);
                }
            }
        }
    }
}
