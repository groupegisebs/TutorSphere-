using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.DTOs.ExpertApproval;

public record ExpertGroupDto(
    Guid Id,
    string Name,
    string? LogoUrl,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? CountryCode,
    bool IsInternational,
    bool IsActive,
    int MemberCount,
    DateTime CreatedAt,
    string? Description = null,
    ExpertGroupLifecycleStatus LifecycleStatus = ExpertGroupLifecycleStatus.Draft,
    Guid? ActiveManagerMandateId = null,
    string? ManagerFullName = null,
    string? ManagerEmail = null,
    string? ManagerPhone = null,
    string? ManagerUserId = null,
    string? BannerUrl = null,
    string? PrimaryColor = null,
    string? SecondaryColor = null,
    bool CanHardDelete = true,
    /// <summary>Experts admis et en activité, Responsable compris.</summary>
    int ActiveExpertCount = 0,
    /// <summary>Experts suspendus : toujours membres, mais sans droits actifs.</summary>
    int SuspendedExpertCount = 0,
    /// <summary>Invitations d'experts encore ouvertes (envoi, candidature, vote, validation).</summary>
    int PendingExpertInviteCount = 0,
    /// <summary>Enseignants dont la fiche a été approuvée par ce groupe.</summary>
    int ApprovedTeacherCount = 0,
    /// <summary>Reçoit les candidatures spontanées qu'aucun pays ne rattache.</summary>
    bool IsDefaultReviewGroup = false,
    /// <summary>Commission TutorSphere sur le net après frais Stripe/PayPal. Le reste (70 % par défaut) va au groupe.</summary>
    decimal PlatformCommissionPercent = 30m);

/// <summary>
/// Ce qu'une suppression de groupe emporterait. Sert à écrire la confirmation : l'administrateur
/// doit voir ce qu'il détruit avant de valider, et non après.
/// </summary>
/// <param name="Detached">
/// Rattachements simplement dénoués : les écoles approuvées, les candidatures orientées et les
/// remarques survivent au groupe, elles perdent seulement la référence.
/// </param>
public record ExpertGroupDeletionImpactDto(
    Guid Id,
    string Name,
    bool IsActive,
    ExpertGroupLifecycleStatus LifecycleStatus,
    IReadOnlyList<ExpertGroupDeletionItemDto> Deleted,
    IReadOnlyList<ExpertGroupDeletionItemDto> Detached)
{
    public int TotalDeleted => Deleted.Sum(d => d.Count);
    public bool IsEmpty => TotalDeleted == 0 && Detached.Sum(d => d.Count) == 0;
}

public record ExpertGroupDeletionItemDto(string Label, int Count);

public record ExpertGroupMemberDto(
    Guid Id,
    Guid ExpertGroupId,
    string UserId,
    string Email,
    string FullName,
    bool AccountCreated = false,
    bool CredentialsSent = false,
    bool NotificationSent = false,
    ExpertGroupMemberRole MemberRole = ExpertGroupMemberRole.Expert,
    ExpertMembershipStatus Status = ExpertMembershipStatus.Active,
    string? Specialty = null);

public record CreateExpertGroupRequest(
    string Name,
    string? ContactEmail,
    string? ContactPhone,
    string? CountryCode,
    bool IsInternational,
    string? LogoUrl = null,
    string? ContactName = null,
    string? Description = null,
    /// <summary>Responsable obligatoire à la création (utilisateur Expert existant).</summary>
    string? ManagerUserId = null,
    string? ManagerEmail = null,
    string? ManagerFirstName = null,
    string? ManagerLastName = null,
    string? ManagerPhone = null,
    string? ManagerFunctionTitle = null,
    DateTime? ManagerMandateStartsAtUtc = null,
    bool CreateManagerAccount = true,
    decimal? PlatformCommissionPercent = null);

public record UpdateExpertGroupRequest(
    string Name,
    string? ContactEmail,
    string? ContactPhone,
    string? LogoUrl,
    bool IsActive,
    string? ContactName = null,
    string? Description = null,
    string? CountryCode = null,
    /// <summary>Portée annoncée du groupe ; null laisse la valeur en place.</summary>
    bool? IsInternational = null,
    /// <summary>Commission plateforme (0–100). Null laisse la valeur en place.</summary>
    decimal? PlatformCommissionPercent = null);

/// <summary>Désignation du groupe qui reçoit les candidatures spontanées.</summary>
public record SetDefaultReviewGroupRequest(bool IsDefault);

public record AddExpertMemberRequest(string UserId);

/// <summary>
/// <paramref name="Invite"/> = true crée le compte si besoin et envoie un mot de passe temporaire.
/// <paramref name="Invite"/> = false ajoute uniquement un compte existant (notification sans MDP).
/// </summary>
public record AddExpertByEmailRequest(
    string Email,
    bool Invite = false,
    string? FirstName = null,
    string? LastName = null);

public record TeacherDocumentDto(
    Guid Id,
    Guid TenantId,
    TeacherDocumentType DocumentType,
    string FileName,
    string FileUrl,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAt,
    string? Notes);

public record PendingTeacherDto(
    Guid TenantId,
    string SchoolName,
    string Slug,
    string? Country,
    string? City,
    ExpertApprovalStatus ApprovalStatus,
    DateTime CreatedAt,
    string? OwnerEmail,
    string? OwnerName,
    int DocumentCount,
    Guid? AssignedExpertGroupId,
    string? AssignedExpertGroupName);

public record TeacherReviewDetailDto(
    Guid TenantId,
    string SchoolName,
    string Slug,
    string? Description,
    string? Country,
    string? City,
    string Language,
    ExpertApprovalStatus ApprovalStatus,
    string? ExpertApprovalNotes,
    DateTime? ExpertApprovedAt,
    Guid? ApprovedByExpertGroupId,
    string? ApprovedByExpertGroupName,
    string? ApprovedByExpertGroupLogoUrl,
    string? OwnerUserId,
    string? OwnerEmail,
    string? OwnerName,
    string? Presentation,
    string? Portfolio,
    string? LogoUrl,
    IReadOnlyList<TeacherDocumentDto> Documents,
    Guid? SuggestedExpertGroupId,
    string? SuggestedExpertGroupName);

public record ExpertDecisionRequest(
    string? Notes,
    string? LicenseSettlement = null,
    string? PromoCode = null,
    bool AutoRenewAtSource = false);

public record InviteTeacherApplicationRequest(
    string Email,
    string? FirstName = null,
    string? PersonalMessage = null);

/// <param name="Language">
/// Langue du message à partager. Le destinataire d'un lien partageable est inconnu du système :
/// c'est l'expert qui choisit, faute de préférence enregistrée.
/// </param>
public record CreateTeacherInviteLinkRequest(
    string? PersonalMessage = null,
    bool Rotate = true,
    string? Language = null);

/// <param name="Language">Langue effective du <paramref name="ShareMessage"/>, normalisée.</param>
/// <param name="EmailSubject">Objet prêt à l'emploi pour un envoi par courriel, dans la même langue.</param>
public record TeacherInviteLinkResponse(
    Guid InviteId,
    string ApplyUrl,
    string ShareMessage,
    DateTime ExpiresAt,
    bool IsNew,
    string? PersonalMessage = null,
    string Language = "fr",
    string EmailSubject = "");

/// <summary>Création directe d'un compte enseignant par un expert / admin de groupe.</summary>
public record RegisterTeacherByExpertRequest(
    /// <summary>E-mail réel de l'enseignant (optionnel) — reçoit les identifiants s'il est renseigné.</summary>
    string? Email = null,
    string FirstName = "",
    string LastName = "",
    /// <summary>Ignoré : un mot de passe temporaire est toujours généré.</summary>
    string? Password = null,
    string? SchoolName = null,
    string? Slug = null,
    string? City = null,
    string? Country = null,
    IReadOnlyList<string>? VisibleCountryCodes = null,
    bool AcceptedTeacherConductPolicy = false,
    /// <summary>IANA — fuseau de l’enseignant (plages horaires).</summary>
    string? TimeZone = null,
    /// <summary>Plages de disponibilité hebdomadaires (plusieurs plages par jour possibles).</summary>
    IReadOnlyList<TeacherAvailabilityRangeDto>? Availabilities = null,
    /// <summary>Offre de service initiale (optionnelle) créée pour le profil.</summary>
    TutorSphere.Application.DTOs.SubscriptionOfferings.CreateSubscriptionOfferingRequest? InitialOffering = null,
    /// <summary>Publie immédiatement la fiche publique (recherche parents / cours).</summary>
    bool PublishPublicProfile = false);

public record TeacherAvailabilityRangeDto(string Day, string StartTime, string EndTime);

public record RegisterTeacherByExpertResponse(
    Guid TenantId,
    string TenantSlug,
    string Email,
    bool CredentialsSent,
    Guid? OfferingId = null,
    string? TemporaryPassword = null,
    string? RealEmail = null,
    bool IsPublicProfile = false,
    string? PublicPath = null);

/// <summary>Groupe d'experts auquel l'utilisateur connecté appartient.</summary>
public record ExpertMyGroupDto(
    Guid Id,
    string Name,
    string? CountryCode,
    string? Description = null,
    bool IsInternational = false,
    int TeacherApprovalTrack = 0,
    string? LogoUrl = null,
    string? BannerUrl = null,
    string? PrimaryColor = null,
    string? SecondaryColor = null);

public record UpdateManagerGroupSettingsRequest(
    string? Description,
    int? TeacherApprovalTrack = null,
    string? PrimaryColor = null,
    string? SecondaryColor = null);

public record TeacherApplicationInviteDto(
    Guid Id,
    string Email,
    string? FirstName,
    TeacherApplicationInviteStatus Status,
    DateTime SentAt,
    DateTime? ExpiresAt,
    DateTime? AcceptedAt,
    Guid? AcceptedTenantId,
    string? InvitedByUserId,
    string? InvitedByName,
    Guid ExpertGroupId,
    string? ExpertGroupName,
    string? SchoolName);

public record TeacherApprovalStatusDto(
    ExpertApprovalStatus Status,
    string? Notes,
    DateTime? ApprovedAt,
    Guid? ExpertGroupId,
    string? ExpertGroupName,
    string? ExpertGroupLogoUrl);
