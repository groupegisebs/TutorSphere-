using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Contracts;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface ITeacherContractService
{
    TeacherContractTemplateDto GetTemplate(string? language = null);
    Task<IReadOnlyList<TeacherContractTeacherOptionDto>> ListTeacherOptionsAsync(
        string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default);
    Task<TeacherContractListItemDto> CreateAndSendAsync(
        string actorUserId,
        SendTeacherContractRequest request,
        bool asPlatformAdmin,
        Guid? actAsGroupId,
        ContractClientContext client,
        CancellationToken ct = default);
    Task<IReadOnlyList<TeacherContractListItemDto>> ListForGroupAsync(
        string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default);
    Task<IReadOnlyList<TeacherContractListItemDto>> ListForTeacherAsync(string teacherUserId, CancellationToken ct = default);
    Task<IReadOnlyList<TeacherContractListItemDto>> ListAllForPlatformAdminAsync(CancellationToken ct = default);
    Task<TeacherContractDetailDto> GetAsync(Guid id, string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default);
    Task CancelAsync(Guid id, string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId, ContractClientContext client, CancellationToken ct = default);
    Task<TeacherContractListItemDto> ResendAsync(Guid id, string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId, ContractClientContext client, CancellationToken ct = default);
    Task<(string FileName, byte[] Bytes)?> GetPdfIfAllowedAsync(
        Guid id, string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId, ContractClientContext client, CancellationToken ct = default);

    Task<TeacherContractSignViewDto> GetByTokenAsync(string token, ContractClientContext client, CancellationToken ct = default);
    Task OpenSectionAsync(string token, string sectionKey, ContractClientContext client, CancellationToken ct = default);
    Task DecideSectionAsync(string token, string sectionKey, DecideContractSectionRequest request, ContractClientContext client, CancellationToken ct = default);
    Task RefuseAsync(string token, RefuseContractRequest request, ContractClientContext client, CancellationToken ct = default);
    Task<TeacherContractDetailDto> CompleteSignatureAsync(string token, CompleteContractSignatureRequest request, ContractClientContext client, CancellationToken ct = default);
    Task<TeacherContractVerifyDto?> VerifyAsync(string contractNumber, CancellationToken ct = default);
}

public sealed class TeacherContractService(
    IApplicationDbContext db,
    IEmailService email,
    IAppUrlProvider urls,
    IUserContactLookup contacts,
    IExpertGroupManagerService managers,
    IExpertGovernanceAuditService audit,
    ITeacherContractPdfWriter pdf,
    ILogger<TeacherContractService> logger) : ITeacherContractService
{
    public TeacherContractTemplateDto GetTemplate(string? language = null)
    {
        var lang = SupportedLanguageCodes.Normalize(language);
        var defaults = TeacherContractCatalog.DefaultVariables(lang);
        var vars = TeacherContractCatalog.VariableKeys.Select(k =>
            new TeacherContractVariableDto(
                k,
                TeacherContractCatalog.VariableLabels.TryGetValue(k, out var label) ? label : k,
                defaults.GetValueOrDefault(k) ?? "")).ToList();
        return new TeacherContractTemplateDto(TeacherContractCatalog.CurrentVersion, vars);
    }

    public async Task<IReadOnlyList<TeacherContractTeacherOptionDto>> ListTeacherOptionsAsync(
        string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default)
    {
        if (asPlatformAdmin && actAsGroupId is null)
        {
            var all = db.Tenants.Where(t => t.OwnerUserId != "").OrderBy(t => t.Name).Take(400).ToList();
            return await MapTeacherOptionsAsync(all, ct);
        }

        var groupId = ResolveActorGroupId(actorUserId, asPlatformAdmin, actAsGroupId);
        var tenants = db.Tenants
            .Where(t => t.ApprovedByExpertGroupId == groupId || (t.ApprovedByExpertGroupId == null && t.OwnerUserId != ""))
            .Where(t => t.ApprovedByExpertGroupId == groupId)
            .OrderBy(t => t.Name)
            .ToList();
        return await MapTeacherOptionsAsync(tenants, ct);
    }

    public async Task<TeacherContractListItemDto> CreateAndSendAsync(
        string actorUserId,
        SendTeacherContractRequest request,
        bool asPlatformAdmin,
        Guid? actAsGroupId,
        ContractClientContext client,
        CancellationToken ct = default)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.Id == request.TenantId)
            ?? throw new InvalidOperationException("Enseignant introuvable.");
        var groupId = ResolveGroupForSend(actorUserId, tenant, asPlatformAdmin, actAsGroupId);
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == groupId)
            ?? throw new InvalidOperationException("Groupe introuvable.");

        var previousList = db.TeacherContracts
            .Where(c => c.TenantId == tenant.Id && c.ExpertGroupId == groupId
                        && (c.Status == TeacherContractStatus.Draft
                            || c.Status == TeacherContractStatus.Sent
                            || c.Status == TeacherContractStatus.Viewed
                            || c.Status == TeacherContractStatus.AwaitingSignature))
            .ToList();

        var teacher = await contacts.GetAsync(tenant.OwnerUserId, ct);
        var manager = await managers.GetActiveManagerAsync(groupId, ct);
        var managerContact = manager is null ? null : await contacts.GetAsync(manager.UserId, ct);
        var language = SupportedLanguageCodes.Normalize(
            string.IsNullOrWhiteSpace(request.Language) ? tenant.Language : request.Language);
        var placeholders = MergePlaceholders(request.Variables, tenant, group, teacher, managerContact, language);
        placeholders["CONTRACT_LANGUAGE"] = language;

        var now = DateTime.UtcNow;
        var contract = new TeacherContract
        {
            ContractNumber = NextNumber(),
            Version = TeacherContractCatalog.CurrentVersion,
            Language = language,
            Status = TeacherContractStatus.Sent,
            TenantId = tenant.Id,
            ExpertGroupId = groupId,
            TeacherUserId = tenant.OwnerUserId,
            CreatedByUserId = actorUserId,
            PlaceholdersJson = JsonSerializer.Serialize(placeholders),
            SignToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
            TokenExpiresAt = now.AddDays(14),
            SentAt = now,
            VerificationCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)),
            ReplacesContractId = previousList.Count > 0 ? previousList[0].Id : null
        };
        db.Add(contract);
        foreach (var previous in previousList)
        {
            previous.Status = TeacherContractStatus.Replaced;
            previous.TokenInvalidatedAt = now;
            previous.ReplacedByContractId = contract.Id;
            previous.UpdatedAt = now;
            AddAudit(previous, TeacherContractAuditAction.Replaced, actorUserId, client, "Remplacé par une nouvelle version.");
        }

        foreach (var section in TeacherContractCatalog.Sections(language))
        {
            db.Add(new TeacherContractSectionDecision
            {
                ContractId = contract.Id,
                SectionKey = section.Key
            });
        }
        AddAudit(contract, TeacherContractAuditAction.Created, actorUserId, client, "Contrat créé.");
        AddAudit(contract, TeacherContractAuditAction.Sent, actorUserId, client, "Lien de signature envoyé (valable 14 jours).");
        await db.SaveChangesAsync(ct);

        var signUrl = SignUrl(contract.SignToken);
        if (!string.IsNullOrWhiteSpace(teacher?.Email))
        {
            await email.SendTeacherContractSignAsync(
                teacher.Value.Email,
                teacher.Value.DisplayName,
                group.Name,
                contract.ContractNumber,
                signUrl,
                contract.TokenExpiresAt!.Value,
                language,
                ct);
        }

        await audit.RecordAsync(
            ExpertGovernanceEventType.TeacherContractSent,
            actorUserId,
            $"Contrat {contract.ContractNumber} envoyé à {placeholders.GetValueOrDefault("TEACHER_FULL_NAME")}.",
            expertGroupId: groupId,
            relatedTenantId: tenant.Id,
            relatedEntityId: contract.Id,
            ct: ct);

        logger.LogInformation("Contrat {Number} envoyé au tenant {TenantId}.", contract.ContractNumber, tenant.Id);
        return MapList(contract, placeholders.GetValueOrDefault("TEACHER_FULL_NAME") ?? tenant.Name, teacher?.Email, group.Name);
    }

    public async Task<IReadOnlyList<TeacherContractListItemDto>> ListForGroupAsync(
        string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default)
    {
        await ExpireOverdueAsync(ct);
        var groupId = ResolveActorGroupId(actorUserId, asPlatformAdmin, actAsGroupId);
        var list = db.TeacherContracts.Where(c => c.ExpertGroupId == groupId)
            .OrderByDescending(c => c.CreatedAt)
            .ToList();
        return MapMany(list);
    }

    public async Task<IReadOnlyList<TeacherContractListItemDto>> ListForTeacherAsync(string teacherUserId, CancellationToken ct = default)
    {
        await ExpireOverdueAsync(ct);
        var tenantIds = db.Tenants.Where(t => t.OwnerUserId == teacherUserId).Select(t => t.Id).ToHashSet();
        var list = db.TeacherContracts.Where(c => tenantIds.Contains(c.TenantId) || c.TeacherUserId == teacherUserId)
            .OrderByDescending(c => c.CreatedAt)
            .ToList();
        return MapMany(list);
    }

    public async Task<IReadOnlyList<TeacherContractListItemDto>> ListAllForPlatformAdminAsync(CancellationToken ct = default)
    {
        await ExpireOverdueAsync(ct);
        var list = db.TeacherContracts.OrderByDescending(c => c.CreatedAt).Take(500).ToList();
        return MapMany(list);
    }

    public async Task<TeacherContractDetailDto> GetAsync(
        Guid id, string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default)
    {
        await ExpireOverdueAsync(ct);
        var contract = RequireVisible(id, actorUserId, asPlatformAdmin, actAsGroupId);
        return MapDetail(contract);
    }

    public async Task CancelAsync(
        Guid id, string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId, ContractClientContext client, CancellationToken ct = default)
    {
        var contract = RequireManage(id, actorUserId, asPlatformAdmin, actAsGroupId);
        EnsureMutable(contract);
        contract.Status = TeacherContractStatus.Cancelled;
        contract.CancelledAt = DateTime.UtcNow;
        contract.TokenInvalidatedAt = DateTime.UtcNow;
        contract.UpdatedAt = DateTime.UtcNow;
        AddAudit(contract, TeacherContractAuditAction.Cancelled, actorUserId, client, "Contrat annulé.");
        await db.SaveChangesAsync(ct);
    }

    public async Task<TeacherContractListItemDto> ResendAsync(
        Guid id, string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId, ContractClientContext client, CancellationToken ct = default)
    {
        var existing = RequireManage(id, actorUserId, asPlatformAdmin, actAsGroupId);
        var vars = TeacherContractCatalog.ParsePlaceholders(existing.PlaceholdersJson);
        return await CreateAndSendAsync(
            actorUserId,
            new SendTeacherContractRequest(existing.TenantId, existing.Language, vars),
            asPlatformAdmin,
            actAsGroupId,
            client,
            ct);
    }

    public async Task<(string FileName, byte[] Bytes)?> GetPdfIfAllowedAsync(
        Guid id, string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId, ContractClientContext client, CancellationToken ct = default)
    {
        var contract = RequireVisible(id, actorUserId, asPlatformAdmin, actAsGroupId);
        if (contract.Status != TeacherContractStatus.Signed || string.IsNullOrWhiteSpace(contract.PdfUrl))
            return null;
        var bytes = await pdf.ReadPdfAsync(contract.PdfUrl, ct);
        if (bytes is null) return null;
        AddAudit(contract, TeacherContractAuditAction.Downloaded, actorUserId, client, "Téléchargement du PDF.");
        await db.SaveChangesAsync(ct);
        return ($"{contract.ContractNumber}.pdf", bytes);
    }

    public async Task<TeacherContractSignViewDto> GetByTokenAsync(string token, ContractClientContext client, CancellationToken ct = default)
    {
        var contract = await RequireTokenAsync(token, ct);
        if (contract.Status == TeacherContractStatus.Sent)
        {
            contract.Status = TeacherContractStatus.Viewed;
            contract.ViewedAt = DateTime.UtcNow;
            contract.UpdatedAt = DateTime.UtcNow;
            AddAudit(contract, TeacherContractAuditAction.Viewed, contract.TeacherUserId, client, "Contrat consulté.");
            await db.SaveChangesAsync(ct);
        }
        return MapSign(contract);
    }

    public async Task OpenSectionAsync(string token, string sectionKey, ContractClientContext client, CancellationToken ct = default)
    {
        var contract = await RequireTokenAsync(token, ct);
        var decision = Decision(contract, sectionKey);
        if (decision.OpenedAt is null)
        {
            decision.OpenedAt = DateTime.UtcNow;
            AddAudit(contract, TeacherContractAuditAction.SectionOpened, contract.TeacherUserId, client,
                $"Section ouverte : {sectionKey}");
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task DecideSectionAsync(
        string token, string sectionKey, DecideContractSectionRequest request, ContractClientContext client, CancellationToken ct = default)
    {
        var contract = await RequireTokenAsync(token, ct);
        var decision = Decision(contract, sectionKey);
        if (decision.OpenedAt is null)
            throw new InvalidOperationException("Vous devez d’abord lire intégralement cette section.");

        if (!request.Accept)
        {
            await MarkRefusedAsync(contract, sectionKey, request.Comment, client, ct);
            return;
        }

        decision.Accepted = true;
        decision.DecidedAt = DateTime.UtcNow;
        decision.Comment = request.Comment;
        decision.UpdatedAt = DateTime.UtcNow;
        AddAudit(contract, TeacherContractAuditAction.SectionAccepted, contract.TeacherUserId, client,
            $"Section acceptée : {sectionKey}");
        if (AllAccepted(contract))
            contract.Status = TeacherContractStatus.AwaitingSignature;
        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RefuseAsync(string token, RefuseContractRequest request, ContractClientContext client, CancellationToken ct = default)
    {
        var contract = await RequireTokenAsync(token, ct);
        await MarkRefusedAsync(contract, request.SectionKey, request.Comment, client, ct);
    }

    public async Task<TeacherContractDetailDto> CompleteSignatureAsync(
        string token, CompleteContractSignatureRequest request, ContractClientContext client, CancellationToken ct = default)
    {
        var contract = await RequireTokenAsync(token, ct);
        if (!AllAccepted(contract))
            throw new InvalidOperationException("Toutes les sections obligatoires doivent être acceptées avant la signature.");
        if (!request.FinalConsent)
            throw new InvalidOperationException("Vous devez confirmer définitivement votre consentement.");
        if (string.IsNullOrWhiteSpace(request.SignaturePngBase64))
            throw new InvalidOperationException("La signature électronique est requise.");

        var expected = TeacherContractCatalog.ParsePlaceholders(contract.PlaceholdersJson)
            .GetValueOrDefault("TEACHER_FULL_NAME") ?? "";
        var typed = (request.TypedFullName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(typed)
            || !string.Equals(NormalizeName(typed), NormalizeName(expected), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Confirmez votre identité en saisissant votre nom et prénom tels qu’ils figurent au contrat.");

        var now = DateTime.UtcNow;
        contract.TeacherTypedName = typed;
        contract.SignaturePngBase64 = request.SignaturePngBase64;
        contract.TeacherIp = client.Ip;
        contract.TeacherUserAgent = client.UserAgent;
        contract.SignedAt = now;
        contract.Status = TeacherContractStatus.Signed;
        contract.TokenInvalidatedAt = now;
        contract.UpdatedAt = now;
        AddAudit(contract, TeacherContractAuditAction.IdentityConfirmed, contract.TeacherUserId, client,
            $"Identité confirmée : {typed}");
        AddAudit(contract, TeacherContractAuditAction.Signed, contract.TeacherUserId, client, "Contrat signé électroniquement.");

        var values = TeacherContractCatalog.ParsePlaceholders(contract.PlaceholdersJson);
        values["CONTRACT_NUMBER"] = contract.ContractNumber;
        values["CONTRACT_VERSION"] = contract.Version;
        values["CONTRACT_LANGUAGE"] = contract.Language;
        values["EFFECTIVE_DATE"] = now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") + " UTC";
        values["TEACHER_SIGNATURE_DATETIME"] = now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
        values["GROUP_SIGNATURE_DATETIME"] = now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
        values["VERIFICATION_REFERENCE"] = contract.VerificationCode ?? contract.ContractNumber;
        var verifyUrl = $"{urls.WebBaseUrl.TrimEnd('/')}/contract/verify/{Uri.EscapeDataString(contract.ContractNumber)}";
        values["VERIFICATION_CODE"] = verifyUrl;

        var sections = TeacherContractCatalog.Sections(contract.Language)
            .Select(s => (s.Title, TeacherContractCatalog.Fill(s.Body, values)))
            .ToList();

        var groupLogoUrl = db.ExpertGroups
            .Where(g => g.Id == contract.ExpertGroupId)
            .Select(g => g.LogoUrl)
            .FirstOrDefault();
        var pendingHash = TeacherContractCatalog.PendingHashNotice(contract.Language);
        var (relativePath, hash) = await pdf.WriteSignedPdfAsync(new TeacherContractPdfModel
        {
            ContractNumber = contract.ContractNumber,
            Version = contract.Version,
            Language = contract.Language,
            GroupName = values.GetValueOrDefault("GROUP_NAME") ?? "",
            TeacherName = values.GetValueOrDefault("TEACHER_FULL_NAME") ?? typed,
            SignedAtUtc = now,
            VerificationCode = contract.VerificationCode ?? contract.ContractNumber,
            VerificationUrl = verifyUrl,
            DocumentHashPlaceholder = pendingHash,
            Sections = sections,
            SignaturePngBase64 = request.SignaturePngBase64,
            GroupSignatoryName = values.GetValueOrDefault("GROUP_SIGNATORY_NAME"),
            GroupSignatoryRole = values.GetValueOrDefault("GROUP_SIGNATORY_ROLE"),
            GroupLogoUrl = groupLogoUrl,
            Chrome = TeacherContractCatalog.PdfChrome(contract.Language)
        }, ct);

        contract.PdfUrl = relativePath;
        contract.DocumentHash = hash;
        values["DOCUMENT_HASH"] = hash;
        contract.PlaceholdersJson = JsonSerializer.Serialize(values);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            ExpertGovernanceEventType.TeacherContractSigned,
            contract.TeacherUserId,
            $"Contrat {contract.ContractNumber} signé par {typed}.",
            expertGroupId: contract.ExpertGroupId,
            relatedTenantId: contract.TenantId,
            relatedEntityId: contract.Id,
            ct: ct);

        return MapDetail(contract);
    }

    public Task<TeacherContractVerifyDto?> VerifyAsync(string contractNumber, CancellationToken ct = default)
    {
        var n = (contractNumber ?? "").Trim();
        var contract = db.TeacherContracts.FirstOrDefault(c => c.ContractNumber == n);
        if (contract is null) return Task.FromResult<TeacherContractVerifyDto?>(null);
        return Task.FromResult<TeacherContractVerifyDto?>(new TeacherContractVerifyDto(
            contract.ContractNumber,
            contract.Version,
            contract.Language,
            contract.Status,
            contract.SignedAt,
            contract.DocumentHash,
            contract.VerificationCode,
            contract.Status == TeacherContractStatus.Signed && !string.IsNullOrWhiteSpace(contract.DocumentHash)));
    }

    private async Task MarkRefusedAsync(
        TeacherContract contract, string sectionKey, string? comment, ContractClientContext client, CancellationToken ct)
    {
        EnsureMutable(contract);
        var decision = Decision(contract, sectionKey);
        decision.Accepted = false;
        decision.DecidedAt = DateTime.UtcNow;
        decision.Comment = comment;
        decision.UpdatedAt = DateTime.UtcNow;
        contract.Status = TeacherContractStatus.Refused;
        contract.RefusedAt = DateTime.UtcNow;
        contract.RefusedSectionKey = sectionKey;
        contract.RefusalComment = comment;
        contract.TokenInvalidatedAt = DateTime.UtcNow;
        contract.UpdatedAt = DateTime.UtcNow;
        AddAudit(contract, TeacherContractAuditAction.SectionRefused, contract.TeacherUserId, client,
            $"Section refusée : {sectionKey}. Contrat marqué Refusé.");
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            ExpertGovernanceEventType.TeacherContractRefused,
            contract.TeacherUserId,
            $"Contrat {contract.ContractNumber} refusé (section {sectionKey}).",
            expertGroupId: contract.ExpertGroupId,
            relatedTenantId: contract.TenantId,
            relatedEntityId: contract.Id,
            ct: ct);
    }

    private async Task<TeacherContract> RequireTokenAsync(string token, CancellationToken ct)
    {
        await ExpireOverdueAsync(ct);
        var trimmed = (token ?? "").Trim();
        var contract = db.TeacherContracts.FirstOrDefault(c => c.SignToken == trimmed)
            ?? throw new InvalidOperationException("Lien de signature invalide.");
        if (contract.TokenInvalidatedAt is not null)
            throw new InvalidOperationException("Ce lien de signature n’est plus valable.");
        if (contract.Status is TeacherContractStatus.Signed or TeacherContractStatus.Refused
            or TeacherContractStatus.Cancelled or TeacherContractStatus.Replaced)
            throw new InvalidOperationException("Ce contrat n’est plus ouvert à la signature.");
        if (contract.Status == TeacherContractStatus.Expired
            || (contract.TokenExpiresAt is DateTime exp && exp < DateTime.UtcNow))
            throw new InvalidOperationException("Ce lien a expiré. Demandez un nouveau lien à l’administrateur du groupe.");
        return contract;
    }

    private TeacherContract RequireVisible(Guid id, string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId)
    {
        var contract = db.TeacherContracts.FirstOrDefault(c => c.Id == id)
            ?? throw new InvalidOperationException("Contrat introuvable.");
        if (asPlatformAdmin) return contract;
        if (string.Equals(contract.TeacherUserId, actorUserId, StringComparison.Ordinal)) return contract;
        var groupId = ResolveActorGroupId(actorUserId, false, actAsGroupId);
        if (contract.ExpertGroupId == groupId) return contract;
        throw new InvalidOperationException("Accès refusé.");
    }

    private TeacherContract RequireManage(Guid id, string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId)
    {
        var contract = RequireVisible(id, actorUserId, asPlatformAdmin, actAsGroupId);
        if (asPlatformAdmin) return contract;
        if (string.Equals(contract.TeacherUserId, actorUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("Seul un administrateur peut effectuer cette action.");
        return contract;
    }

    private Guid ResolveActorGroupId(string actorUserId, bool asPlatformAdmin, Guid? actAsGroupId)
    {
        if (asPlatformAdmin && actAsGroupId is Guid g) return g;
        var mandate = db.ExpertGroupManagerMandates.FirstOrDefault(m =>
            m.UserId == actorUserId && m.Status == ExpertGroupManagerMandateStatus.Active);
        if (mandate is not null) return mandate.ExpertGroupId;
        throw new InvalidOperationException("Accès réservé à l’administrateur du groupe.");
    }

    private Guid ResolveGroupForSend(string actorUserId, Tenant tenant, bool asPlatformAdmin, Guid? actAsGroupId)
    {
        if (asPlatformAdmin)
        {
            if (actAsGroupId is Guid g) return g;
            if (tenant.ApprovedByExpertGroupId is Guid bound) return bound;
            throw new InvalidOperationException("Cet enseignant n’est rattaché à aucun groupe.");
        }

        var actorGroup = ResolveActorGroupId(actorUserId, false, null);
        if (tenant.ApprovedByExpertGroupId is Guid assigned && assigned != actorGroup)
            throw new InvalidOperationException("Cet enseignant n’appartient pas à votre groupe.");
        return actorGroup;
    }

    private async Task ExpireOverdueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var stale = db.TeacherContracts.Where(c =>
            c.TokenInvalidatedAt == null
            && c.TokenExpiresAt != null && c.TokenExpiresAt < now
            && (c.Status == TeacherContractStatus.Sent
                || c.Status == TeacherContractStatus.Viewed
                || c.Status == TeacherContractStatus.AwaitingSignature
                || c.Status == TeacherContractStatus.Draft))
            .ToList();
        foreach (var c in stale)
        {
            c.Status = TeacherContractStatus.Expired;
            c.ExpiredAt = now;
            c.TokenInvalidatedAt = now;
            c.UpdatedAt = now;
            AddAudit(c, TeacherContractAuditAction.Expired, null, new ContractClientContext(null, null), "Lien expiré.");
        }
        if (stale.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private static void EnsureMutable(TeacherContract contract)
    {
        if (contract.Status is TeacherContractStatus.Signed or TeacherContractStatus.Cancelled or TeacherContractStatus.Replaced)
            throw new InvalidOperationException("Ce contrat ne peut plus être modifié.");
    }

    private TeacherContractSectionDecision Decision(TeacherContract contract, string sectionKey)
    {
        return db.TeacherContractSectionDecisions.FirstOrDefault(s => s.ContractId == contract.Id && s.SectionKey == sectionKey)
            ?? throw new InvalidOperationException("Section introuvable.");
    }

    private bool AllAccepted(TeacherContract contract)
    {
        var keys = TeacherContractCatalog.Sections(contract.Language).Select(s => s.Key).ToHashSet();
        var decisions = db.TeacherContractSectionDecisions.Where(d => d.ContractId == contract.Id).ToList();
        return keys.All(k => decisions.Any(d => d.SectionKey == k && d.Accepted == true));
    }

    private string NextNumber()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"TS-C-{year}-";
        var last = db.TeacherContracts
            .Where(c => c.ContractNumber.StartsWith(prefix))
            .Select(c => c.ContractNumber)
            .ToList()
            .Select(n => int.TryParse(n[prefix.Length..], out var i) ? i : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{(last + 1):D5}";
    }

    private string SignUrl(string token) =>
        $"{urls.WebBaseUrl.TrimEnd('/')}/contract/sign/{Uri.EscapeDataString(token)}";

    private Dictionary<string, string> MergePlaceholders(
        Dictionary<string, string>? incoming,
        Tenant tenant,
        ExpertGroup group,
        (string Email, string DisplayName)? teacher,
        (string Email, string DisplayName)? manager,
        string language)
    {
        var values = TeacherContractCatalog.DefaultVariables(language);
        if (incoming is not null)
        {
            foreach (var (k, v) in incoming)
                if (!string.IsNullOrWhiteSpace(v)) values[k] = v.Trim();
        }
        values["GROUP_NAME"] = group.Name;
        values["GROUP_ADMIN_NAME"] = manager?.DisplayName ?? group.ContactName ?? "Responsable du groupe";
        values["GROUP_SIGNATORY_NAME"] = values["GROUP_ADMIN_NAME"];
        values["GROUP_SIGNATORY_ROLE"] = values.GetValueOrDefault("GROUP_ADMIN_ROLE") ?? "Responsable du groupe";
        values["TEACHER_FULL_NAME"] = teacher?.DisplayName ?? tenant.Name;
        values["TEACHER_EMAIL"] = teacher?.Email ?? "—";
        values["TEACHER_COUNTRY"] = tenant.Country ?? "—";
        values["TEACHER_PHONE"] = values.GetValueOrDefault("TEACHER_PHONE") ?? "—";
        values["TEACHER_SUBJECTS"] = values.GetValueOrDefault("TEACHER_SUBJECTS") ?? "Selon le profil TutorSphere";
        values["TEACHER_LEVELS"] = values.GetValueOrDefault("TEACHER_LEVELS") ?? "Selon le profil TutorSphere";
        values["CONTRACT_VERSION"] = TeacherContractCatalog.CurrentVersion;
        values["CONTRACT_LANGUAGE"] = language;
        values["CURRENCY"] = string.IsNullOrWhiteSpace(tenant.Currency) ? values["CURRENCY"] : tenant.Currency;
        return values;
    }

    private async Task<IReadOnlyList<TeacherContractTeacherOptionDto>> MapTeacherOptionsAsync(
        List<Tenant> tenants, CancellationToken ct)
    {
        var groupIds = tenants.Where(t => t.ApprovedByExpertGroupId.HasValue)
            .Select(t => t.ApprovedByExpertGroupId!.Value).Distinct().ToList();
        var groups = db.ExpertGroups.Where(g => groupIds.Contains(g.Id)).ToDictionary(g => g.Id, g => g.Name);
        var result = new List<TeacherContractTeacherOptionDto>(tenants.Count);
        foreach (var t in tenants)
        {
            var teacher = string.IsNullOrWhiteSpace(t.OwnerUserId) ? null : await contacts.GetAsync(t.OwnerUserId, ct);
            groups.TryGetValue(t.ApprovedByExpertGroupId ?? Guid.Empty, out var gn);
            result.Add(new TeacherContractTeacherOptionDto(
                t.Id,
                teacher?.DisplayName ?? t.Name,
                teacher?.Email,
                gn,
                SupportedLanguageCodes.Normalize(t.Language)));
        }
        return result;
    }

    private IReadOnlyList<TeacherContractListItemDto> MapMany(List<TeacherContract> list)
    {
        var tenantIds = list.Select(c => c.TenantId).Distinct().ToList();
        var groupIds = list.Select(c => c.ExpertGroupId).Distinct().ToList();
        var tenants = db.Tenants.Where(t => tenantIds.Contains(t.Id)).ToDictionary(t => t.Id);
        var groups = db.ExpertGroups.Where(g => groupIds.Contains(g.Id)).ToDictionary(g => g.Id, g => g.Name);
        var result = new List<TeacherContractListItemDto>(list.Count);
        foreach (var c in list)
        {
            tenants.TryGetValue(c.TenantId, out var t);
            groups.TryGetValue(c.ExpertGroupId, out var gn);
            var parsed = TeacherContractCatalog.ParsePlaceholders(c.PlaceholdersJson);
            var name = parsed.GetValueOrDefault("TEACHER_FULL_NAME") ?? t?.Name ?? "Enseignant";
            var email = parsed.GetValueOrDefault("TEACHER_EMAIL");
            result.Add(MapList(c, name, email, gn ?? "Groupe"));
        }
        return result;
    }

    private static TeacherContractListItemDto MapList(TeacherContract c, string teacherName, string? email, string groupName) =>
        new(c.Id, c.ContractNumber, c.Version, c.Language, c.Status, c.TenantId, teacherName, email, groupName,
            c.CreatedAt, c.SentAt, c.SignedAt, c.TokenExpiresAt, c.Status == TeacherContractStatus.Signed && !string.IsNullOrWhiteSpace(c.PdfUrl));

    private TeacherContractDetailDto MapDetail(TeacherContract contract)
    {
        var values = TeacherContractCatalog.ParsePlaceholders(contract.PlaceholdersJson);
        values["CONTRACT_NUMBER"] = contract.ContractNumber;
        values["CONTRACT_VERSION"] = contract.Version;
        values["CONTRACT_LANGUAGE"] = contract.Language;
        var decisions = db.TeacherContractSectionDecisions.Where(d => d.ContractId == contract.Id).ToList();
        var sections = TeacherContractCatalog.Sections(contract.Language).Select(s =>
        {
            var d = decisions.FirstOrDefault(x => x.SectionKey == s.Key);
            return new TeacherContractSectionDto(
                s.Key, s.Title, TeacherContractCatalog.Fill(s.Body, values),
                d?.OpenedAt is not null, d?.Accepted);
        }).ToList();
        var audits = db.TeacherContractAuditEvents.Where(a => a.ContractId == contract.Id)
            .OrderBy(a => a.CreatedAt)
            .ToList();
        return new TeacherContractDetailDto(
            contract.Id, contract.ContractNumber, contract.Version, contract.Language, contract.Status,
            values.GetValueOrDefault("GROUP_NAME") ?? "",
            values.GetValueOrDefault("TEACHER_FULL_NAME") ?? "",
            contract.TokenExpiresAt, contract.SignedAt, null, contract.DocumentHash,
            contract.VerificationCode,
            $"{urls.WebBaseUrl.TrimEnd('/')}/contract/verify/{Uri.EscapeDataString(contract.ContractNumber)}",
            contract.TokenInvalidatedAt is null && !string.IsNullOrWhiteSpace(contract.SignToken)
                ? SignUrl(contract.SignToken) : "",
            sections,
            audits.Select(a => new TeacherContractAuditDto(a.CreatedAt, a.Action, a.Summary, null)).ToList(),
            sections.All(s => s.Accepted == true),
            contract.RefusalComment);
    }

    private TeacherContractSignViewDto MapSign(TeacherContract contract)
    {
        var d = MapDetail(contract);
        var logo = db.ExpertGroups.Where(g => g.Id == contract.ExpertGroupId).Select(g => g.LogoUrl).FirstOrDefault();
        return new TeacherContractSignViewDto(
            d.Id, d.ContractNumber, d.Version, d.Language, d.Status, d.GroupName, d.TeacherName,
            d.TokenExpiresAt, d.Sections, d.AllSectionsAccepted, d.TeacherName, logo);
    }

    private void AddAudit(
        TeacherContract contract, TeacherContractAuditAction action, string? actor, ContractClientContext client, string summary)
    {
        db.Add(new TeacherContractAuditEvent
        {
            ContractId = contract.Id,
            Action = action,
            ActorUserId = actor,
            IpAddress = Trunc(client.Ip, 64),
            UserAgent = Trunc(client.UserAgent, 400),
            Summary = summary
        });
    }

    private static string? Trunc(string? s, int n) =>
        string.IsNullOrWhiteSpace(s) ? null : (s.Length <= n ? s : s[..n]);

    private static string NormalizeName(string s) =>
        string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
