using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Settings;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Persistence;

namespace TutorSphere.Infrastructure.WhatsApp;

public sealed class WhatsAppEnrollmentService : IWhatsAppEnrollmentService
{
    private readonly ApplicationDbContext _db;
    private readonly WhatsAppGatewayClient _gateway;
    private readonly WhatsAppChannelOptions _options;
    private readonly ILogger<WhatsAppEnrollmentService> _logger;

    // PBKDF2 salé : un code à six chiffres se retrouverait sinon par simple table de hachage.
    private readonly PasswordHasher<WhatsAppEnrollment> _hasher = new();

    public WhatsAppEnrollmentService(
        ApplicationDbContext db,
        WhatsAppGatewayClient gateway,
        IOptions<WhatsAppChannelOptions> options,
        ILogger<WhatsAppEnrollmentService> logger)
    {
        _db = db;
        _gateway = gateway;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WhatsAppChannelDto> GetAsync(string userId, CancellationToken ct = default)
    {
        var enrollment = await FindAsync(userId, ct);
        return ToDto(enrollment, _options.MaxVerificationAttempts);
    }

    public async Task<WhatsAppChannelResult> StartAsync(
        string userId, string phone, CancellationToken ct = default)
    {
        if (!_gateway.IsConfigured)
            return Failure("Canal WhatsApp indisponible : la passerelle n'est pas configurée.", null);

        if (!PhoneE164.TryNormalize(phone, _options.DefaultCountryCode, out var normalized, out var phoneError))
            return Failure(phoneError!, null);

        var enrollment = await FindAsync(userId, ct);
        var now = DateTime.UtcNow;

        if (enrollment is not null
            && enrollment.Status == WhatsAppEnrollmentStatus.Active
            && enrollment.PhoneE164 == normalized)
        {
            return new WhatsAppChannelResult(true, null, ToDto(enrollment, _options.MaxVerificationAttempts));
        }

        if (enrollment?.VerificationSentAt is DateTime sentAt)
        {
            var wait = TimeSpan.FromSeconds(_options.ResendCooldownSeconds) - (now - sentAt);
            if (wait > TimeSpan.Zero)
            {
                return Failure(
                    $"Un code vient d'être envoyé. Patientez {Math.Ceiling(wait.TotalSeconds)} seconde(s).",
                    ToDto(enrollment, _options.MaxVerificationAttempts));
            }
        }

        var language = await ResolveLanguageAsync(userId, ct);

        if (enrollment is null)
        {
            enrollment = new WhatsAppEnrollment { UserId = userId };
            _db.WhatsAppEnrollmentsSet.Add(enrollment);
        }

        // Changement de numéro : le canal repart de zéro, sinon l'ancien numéro resterait notifiable.
        enrollment.PhoneE164 = normalized;
        enrollment.Language = language;
        enrollment.Status = WhatsAppEnrollmentStatus.PendingVerification;
        enrollment.VerifiedAt = null;
        enrollment.OptOutAt = null;
        enrollment.VerificationAttempts = 0;
        enrollment.VerificationSentAt = now;
        enrollment.VerificationExpiresAt = now.AddMinutes(_options.CodeLifetimeMinutes);
        enrollment.UpdatedAt = now;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        enrollment.VerificationCodeHash = _hasher.HashPassword(enrollment, code);

        await _db.SaveChangesAsync(ct);

        var sent = await _gateway.SendTemplateAsync(
            normalized,
            WhatsAppTemplates.VerificationCode,
            new Dictionary<string, string> { ["Code"] = code },
            language,
            ct);

        if (!sent.Success)
        {
            _logger.LogWarning(
                "Envoi du code WhatsApp refusé pour l'utilisateur {UserId} : {Error}", userId, sent.Error);
            return Failure(
                sent.Error ?? "Le code n'a pas pu être envoyé sur WhatsApp.",
                ToDto(enrollment, _options.MaxVerificationAttempts));
        }

        return new WhatsAppChannelResult(true, null, ToDto(enrollment, _options.MaxVerificationAttempts));
    }

    public async Task<WhatsAppChannelResult> ConfirmAsync(
        string userId, string code, CancellationToken ct = default)
    {
        var enrollment = await FindAsync(userId, ct);
        if (enrollment is null || string.IsNullOrWhiteSpace(enrollment.VerificationCodeHash))
            return Failure("Aucune vérification en cours. Demandez un nouveau code.", null);

        WhatsAppChannelDto Dto() => ToDto(enrollment, _options.MaxVerificationAttempts);

        if (enrollment.VerificationExpiresAt is DateTime expiry && expiry < DateTime.UtcNow)
            return Failure("Ce code a expiré. Demandez-en un nouveau.", Dto());

        if (enrollment.VerificationAttempts >= _options.MaxVerificationAttempts)
            return Failure("Trop de tentatives. Demandez un nouveau code.", Dto());

        var verification = _hasher.VerifyHashedPassword(
            enrollment, enrollment.VerificationCodeHash, (code ?? string.Empty).Trim());

        if (verification == PasswordVerificationResult.Failed)
        {
            enrollment.VerificationAttempts++;
            enrollment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            var left = Math.Max(0, _options.MaxVerificationAttempts - enrollment.VerificationAttempts);
            return Failure($"Code incorrect. {left} tentative(s) restante(s).", Dto());
        }

        var now = DateTime.UtcNow;
        enrollment.Status = WhatsAppEnrollmentStatus.Active;
        enrollment.VerifiedAt = now;
        enrollment.ConsentAt ??= now;
        enrollment.ConsentSource ??= "parent-settings";
        enrollment.VerificationCodeHash = null;
        enrollment.VerificationExpiresAt = null;
        enrollment.VerificationAttempts = 0;
        enrollment.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return new WhatsAppChannelResult(true, null, Dto());
    }

    public async Task<WhatsAppChannelResult> SetPreferencesAsync(
        string userId, bool lessonReminders, CancellationToken ct = default)
    {
        var enrollment = await FindAsync(userId, ct);
        if (enrollment is null)
            return Failure("Aucun numéro WhatsApp enregistré.", null);

        enrollment.LessonReminders = lessonReminders;
        enrollment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new WhatsAppChannelResult(true, null, ToDto(enrollment, _options.MaxVerificationAttempts));
    }

    public async Task<WhatsAppChannelResult> OptOutAsync(string userId, CancellationToken ct = default)
    {
        var enrollment = await FindAsync(userId, ct);
        if (enrollment is null)
            return new WhatsAppChannelResult(true, null, ToDto(null, _options.MaxVerificationAttempts));

        var now = DateTime.UtcNow;
        enrollment.Status = WhatsAppEnrollmentStatus.OptedOut;
        enrollment.OptOutAt = now;
        enrollment.VerificationCodeHash = null;
        enrollment.VerificationExpiresAt = null;
        enrollment.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return new WhatsAppChannelResult(true, null, ToDto(enrollment, _options.MaxVerificationAttempts));
    }

    private Task<WhatsAppEnrollment?> FindAsync(string userId, CancellationToken ct) =>
        _db.WhatsAppEnrollmentsSet.FirstOrDefaultAsync(e => e.UserId == userId, ct);

    private async Task<string> ResolveLanguageAsync(string userId, CancellationToken ct)
    {
        var language = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.PreferredLanguage)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(language) ? "fr" : language;
    }

    private static WhatsAppChannelResult Failure(string error, WhatsAppChannelDto? channel) =>
        new(false, error, channel);

    private static WhatsAppChannelDto ToDto(WhatsAppEnrollment? enrollment, int maxAttempts)
    {
        if (enrollment is null)
            return new WhatsAppChannelDto("None", null, false, null, null, null, maxAttempts);

        return new WhatsAppChannelDto(
            enrollment.Status.ToString(),
            PhoneE164.Mask(enrollment.PhoneE164),
            enrollment.LessonReminders,
            enrollment.VerifiedAt,
            enrollment.ConsentAt,
            enrollment.VerificationExpiresAt,
            Math.Max(0, maxAttempts - enrollment.VerificationAttempts));
    }
}
