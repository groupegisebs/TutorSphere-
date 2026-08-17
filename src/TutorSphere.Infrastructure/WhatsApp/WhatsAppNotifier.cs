using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Persistence;

namespace TutorSphere.Infrastructure.WhatsApp;

public sealed class WhatsAppNotifier : IWhatsAppNotifier
{
    private readonly ApplicationDbContext _db;
    private readonly WhatsAppGatewayClient _gateway;
    private readonly ILogger<WhatsAppNotifier> _logger;

    public WhatsAppNotifier(
        ApplicationDbContext db,
        WhatsAppGatewayClient gateway,
        ILogger<WhatsAppNotifier> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<bool> AcceptsLessonRemindersAsync(string userId, CancellationToken ct = default)
    {
        if (!_gateway.IsConfigured || string.IsNullOrWhiteSpace(userId))
            return false;

        return await _db.WhatsAppEnrollmentsSet.AsNoTracking().AnyAsync(
            e => e.UserId == userId
                 && e.Status == WhatsAppEnrollmentStatus.Active
                 && e.VerifiedAt != null
                 && e.LessonReminders,
            ct);
    }

    public async Task SendLessonReminderAsync(
        string userId,
        string recipientName,
        string tutorName,
        string subject,
        DateTime lessonDate,
        CancellationToken ct = default)
    {
        if (!_gateway.IsConfigured || string.IsNullOrWhiteSpace(userId))
            return;

        var enrollment = await _db.WhatsAppEnrollmentsSet.AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.UserId == userId
                     && e.Status == WhatsAppEnrollmentStatus.Active
                     && e.VerifiedAt != null
                     && e.LessonReminders,
                ct);

        if (enrollment is null)
            return;

        var culture = ResolveCulture(enrollment.Language);
        var result = await _gateway.SendTemplateAsync(
            enrollment.PhoneE164,
            WhatsAppTemplates.LessonReminder,
            new Dictionary<string, string>
            {
                ["RecipientName"] = recipientName,
                ["TutorName"] = tutorName,
                ["Subject"] = subject,
                ["LessonDate"] = lessonDate.ToString("f", culture)
            },
            enrollment.Language,
            ct);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Rappel WhatsApp non délivré à l'utilisateur {UserId} : {Error}", userId, result.Error);
        }
    }

    private static CultureInfo ResolveCulture(string? language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(language) ? "fr" : language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("fr");
        }
    }
}
