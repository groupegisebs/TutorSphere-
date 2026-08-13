using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Parents;
using TutorSphere.Domain.Entities;

namespace TutorSphere.Application.Services;

public interface IParentEngagementService
{
    Task<ParentReferralDto> GetOrCreateReferralAsync(string parentUserId, CancellationToken ct = default);
    Task ApplyReferralCodeAsync(string newParentUserId, string? referralCode, CancellationToken ct = default);
    Task<ParentSupportRequestDto> SubmitSupportAsync(
        string parentUserId,
        CreateParentSupportRequest request,
        CancellationToken ct = default);
}

public sealed class ParentEngagementService(
    IApplicationDbContext db,
    IAppUrlProvider urls,
    IEmailService email,
    IConfiguration configuration) : IParentEngagementService
{
    public async Task<ParentReferralDto> GetOrCreateReferralAsync(string parentUserId, CancellationToken ct = default)
    {
        var parent = db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUserId)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        if (string.IsNullOrWhiteSpace(parent.ReferralCode))
        {
            parent.ReferralCode = AllocateUniqueCode();
            parent.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var successful = db.ParentProfilesForAnyTenant.Count(p => p.ReferredByParentProfileId == parent.Id);
        var web = urls.WebBaseUrl.TrimEnd('/');
        var shareUrl = $"{web}/register/parent?ref={Uri.EscapeDataString(parent.ReferralCode!)}";

        return new ParentReferralDto(
            parent.ReferralCode!,
            shareUrl,
            parent.ReferralRewardMonths,
            successful,
            "1 mois gratuit pour votre proche et 1 mois pour vous à chaque inscription réussie.");
    }

    public async Task ApplyReferralCodeAsync(string newParentUserId, string? referralCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(referralCode))
            return;

        var code = referralCode.Trim().ToUpperInvariant();
        var newParent = db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == newParentUserId);
        if (newParent is null)
            return;

        if (newParent.ReferredByParentProfileId is not null)
            return;

        var referrer = db.ParentProfilesForAnyTenant.FirstOrDefault(p =>
            p.ReferralCode != null
            && p.ReferralCode.ToUpper() == code
            && p.Id != newParent.Id);

        if (referrer is null)
            return;

        newParent.ReferredByParentProfileId = referrer.Id;
        newParent.ReferralRewardMonths += 1; // filleul : 1 mois
        referrer.ReferralRewardMonths += 1;  // parrain : 1 mois
        newParent.UpdatedAt = DateTime.UtcNow;
        referrer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ParentSupportRequestDto> SubmitSupportAsync(
        string parentUserId,
        CreateParentSupportRequest request,
        CancellationToken ct = default)
    {
        var parent = db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == parentUserId)
            ?? throw new InvalidOperationException("Profil parent introuvable.");

        var subject = (request.Subject ?? "").Trim();
        var message = (request.Message ?? "").Trim();
        if (subject.Length < 3)
            throw new InvalidOperationException("Objet trop court.");
        if (message.Length < 10)
            throw new InvalidOperationException("Message trop court (10 caractères minimum).");

        var entity = new ParentSupportRequest
        {
            ParentProfileId = parent.Id,
            UserId = parentUserId,
            Subject = subject[..Math.Min(subject.Length, 200)],
            Message = message[..Math.Min(message.Length, 4000)],
            ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail)
                ? parent.Email
                : request.ContactEmail.Trim(),
            Status = ParentSupportRequestStatus.Open
        };
        db.Add(entity);
        await db.SaveChangesAsync(ct);

        var supportTo = configuration["Support:Email"]
            ?? configuration["MailGateway:FromAddress"]
            ?? parent.Email;

        try
        {
            await email.SendSupportContactAsync(
                supportTo,
                parent.FirstName,
                parent.LastName,
                entity.ContactEmail ?? parent.Email,
                entity.Subject,
                entity.Message,
                ct);
        }
        catch
        {
            // La demande reste enregistrée même si l'e-mail échoue.
        }

        return new ParentSupportRequestDto(
            entity.Id, entity.Subject, entity.Message, (int)entity.Status, entity.CreatedAt);
    }

    private string AllocateUniqueCode()
    {
        for (var i = 0; i < 20; i++)
        {
            var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(3));
            var code = $"FAM-{suffix}";
            if (!db.ParentProfilesForAnyTenant.Any(p => p.ReferralCode == code))
                return code;
        }

        return $"FAM-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
    }
}
