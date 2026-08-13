using Microsoft.Extensions.Logging;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public class ExpertMembershipGovernanceService(
    IApplicationDbContext db,
    IEmailService email,
    IUserContactLookup contacts,
    IAppUrlProvider urls,
    IExpertIdentityActions identity,
    ILogger<ExpertMembershipGovernanceService> logger) : IExpertMembershipGovernanceService
{
    private const int InviteDays = 30;
    private const int VoteDays = 15;

    public async Task<ExpertMembershipInviteDto> CreateInviteAsync(
        string initiatorUserId,
        CreateExpertMembershipInviteRequest request,
        CancellationToken ct = default)
    {
        var membership = RequireActiveMembership(initiatorUserId);
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == membership.ExpertGroupId && g.IsActive)
            ?? throw new InvalidOperationException("Groupe d'experts introuvable ou inactif.");

        var emailAddr = NormalizeEmail(request.Email);
        var firstName = RequireText(request.FirstName, "Prénom");
        var lastName = RequireText(request.LastName, "Nom");

        if (db.ExpertMembershipInvites.Any(i =>
                i.ExpertGroupId == group.Id
                && i.Email == emailAddr
                && (i.Status == ExpertMembershipInviteStatus.Sent
                    || i.Status == ExpertMembershipInviteStatus.AcceptedByCandidate
                    || i.Status == ExpertMembershipInviteStatus.PendingMemberApproval
                    || i.Status == ExpertMembershipInviteStatus.AwaitingAdminValidation)))
            throw new InvalidOperationException("Une invitation active existe déjà pour cette adresse.");

        var existingUserId = await identity.FindUserIdByEmailAsync(emailAddr, ct);
        if (existingUserId is not null
            && db.ExpertGroupMembers.Any(m =>
                m.UserId == existingUserId
                && (m.Status == ExpertMembershipStatus.Active || m.Status == ExpertMembershipStatus.Suspended)))
            throw new InvalidOperationException("Ce compte appartient déjà à un groupe d'experts.");

        var token = Guid.NewGuid().ToString("N");
        var invite = new ExpertMembershipInvite
        {
            ExpertGroupId = group.Id,
            InvitedByUserId = initiatorUserId,
            Email = emailAddr,
            FirstName = firstName,
            LastName = lastName,
            Phone = TrimOrNull(request.Phone),
            Specialty = TrimOrNull(request.Specialty),
            IntendedRole = TrimOrNull(request.IntendedRole),
            Presentation = TrimOrNull(request.Presentation),
            Justification = TrimOrNull(request.Justification),
            PersonalMessage = TrimOrNull(request.PersonalMessage),
            Token = token,
            SentAtUtc = DateTime.UtcNow,
            InviteExpiresAtUtc = DateTime.UtcNow.AddDays(InviteDays),
            Status = ExpertMembershipInviteStatus.Sent
        };
        db.Add(invite);
        await db.SaveChangesAsync(ct);

        var initiator = await contacts.GetAsync(initiatorUserId, ct);
        var joinUrl = $"{urls.WebBaseUrl.TrimEnd('/')}/expert/join?invite={Uri.EscapeDataString(token)}";
        try
        {
            await email.SendExpertMembershipInviteAsync(
                emailAddr,
                firstName,
                initiator?.DisplayName ?? "un expert TutorSphere",
                group.Name,
                request.PersonalMessage ?? "",
                joinUrl,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Échec envoi invitation membership {InviteId}", invite.Id);
        }

        return await MapInviteAsync(invite, initiatorUserId, ct);
    }

    public async Task<IReadOnlyList<ExpertMembershipInviteDto>> ListForExpertAsync(
        string expertUserId,
        CancellationToken ct = default)
    {
        var membership = RequireActiveMembership(expertUserId);
        ExpireStaleInvites(membership.ExpertGroupId);
        var invites = db.ExpertMembershipInvites
            .Where(i => i.ExpertGroupId == membership.ExpertGroupId)
            .OrderByDescending(i => i.SentAtUtc)
            .Take(100)
            .ToList();
        var result = new List<ExpertMembershipInviteDto>();
        foreach (var i in invites)
            result.Add(await MapInviteAsync(i, expertUserId, ct));
        return result;
    }

    public async Task<IReadOnlyList<ExpertMembershipInviteDto>> ListForAdminAsync(
        Guid? groupId,
        CancellationToken ct = default)
    {
        if (groupId is Guid gid)
            ExpireStaleInvites(gid);
        else
        {
            foreach (var id in db.ExpertGroups.Select(g => g.Id).ToList())
                ExpireStaleInvites(id);
        }

        var q = db.ExpertMembershipInvites.AsQueryable();
        if (groupId is Guid g)
            q = q.Where(i => i.ExpertGroupId == g);

        var invites = q.OrderByDescending(i => i.SentAtUtc).Take(200).ToList();
        var result = new List<ExpertMembershipInviteDto>();
        foreach (var i in invites)
            result.Add(await MapInviteAsync(i, viewerUserId: null, ct));
        return result;
    }

    public Task<IReadOnlyList<ExpertGroupMemberListItemDto>> ListActiveMembersAsync(
        string expertUserId,
        CancellationToken ct = default)
    {
        var membership = RequireActiveMembership(expertUserId);
        var members = db.ExpertGroupMembers
            .Where(m => m.ExpertGroupId == membership.ExpertGroupId
                        && m.Status != ExpertMembershipStatus.Removed)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        IReadOnlyList<ExpertGroupMemberListItemDto> result = members
            .Select(m => new ExpertGroupMemberListItemDto(
                m.Id, m.ExpertGroupId, m.UserId, "", "", m.Status, m.AdmissionMethod,
                m.Specialty, m.AdmittedAtUtc))
            .ToList();
        return Task.FromResult(result);
    }

    public async Task<ExpertMembershipInvitePublicDto> GetPublicInviteAsync(string token, CancellationToken ct = default)
    {
        var invite = GetByToken(token);
        ExpireIfNeeded(invite);
        if (invite.Status is ExpertMembershipInviteStatus.Expired or ExpertMembershipInviteStatus.Cancelled
            or ExpertMembershipInviteStatus.Rejected or ExpertMembershipInviteStatus.Approved)
            throw new InvalidOperationException("Cette invitation n'est plus valide.");

        if (invite.InviteExpiresAtUtc < DateTime.UtcNow
            && invite.Status == ExpertMembershipInviteStatus.Sent)
        {
            invite.Status = ExpertMembershipInviteStatus.Expired;
            invite.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Cette invitation a expiré.");
        }

        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == invite.ExpertGroupId)
            ?? throw new InvalidOperationException("Groupe introuvable.");
        var inviter = await contacts.GetAsync(invite.InvitedByUserId, ct);
        var needsAccount = await identity.FindUserIdByEmailAsync(invite.Email, ct) is null;

        return new ExpertMembershipInvitePublicDto(
            invite.Id,
            group.Name,
            group.CountryCode,
            inviter?.DisplayName ?? "Expert",
            invite.Email,
            invite.FirstName,
            invite.LastName,
            invite.Status,
            invite.InviteExpiresAtUtc,
            needsAccount);
    }

    public async Task<ExpertMembershipInviteDto> SubmitCandidacyAsync(
        SubmitExpertMembershipCandidacyRequest request,
        CancellationToken ct = default)
    {
        if (!request.AcceptedConduct || !request.AcceptedPrivacy)
            throw new InvalidOperationException("Vous devez accepter le code de conduite et la politique de confidentialité.");

        var invite = GetByToken(request.Token);
        ExpireIfNeeded(invite);
        if (invite.Status is not (ExpertMembershipInviteStatus.Sent or ExpertMembershipInviteStatus.AcceptedByCandidate))
            throw new InvalidOperationException("Cette invitation ne peut plus être soumise.");
        if (invite.InviteExpiresAtUtc < DateTime.UtcNow)
        {
            invite.Status = ExpertMembershipInviteStatus.Expired;
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Cette invitation a expiré.");
        }

        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == invite.ExpertGroupId && g.IsActive)
            ?? throw new InvalidOperationException("Le groupe n'est plus actif.");

        var firstName = string.IsNullOrWhiteSpace(request.FirstName) ? invite.FirstName : request.FirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(request.LastName) ? invite.LastName : request.LastName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Phone)) invite.Phone = request.Phone.Trim();
        if (!string.IsNullOrWhiteSpace(request.Specialty)) invite.Specialty = request.Specialty.Trim();
        if (!string.IsNullOrWhiteSpace(request.Presentation)) invite.Presentation = request.Presentation.Trim();
        invite.FirstName = firstName;
        invite.LastName = lastName;

        var userId = await identity.EnsureCandidateUserAsync(
            invite.Email, firstName, lastName, request.Password, ct);
        invite.CandidateUserId = userId;
        invite.ConductAccepted = true;
        invite.PrivacyAccepted = true;
        invite.CandidateSubmittedAtUtc = DateTime.UtcNow;
        invite.Status = ExpertMembershipInviteStatus.AcceptedByCandidate;

        // Snapshot eligible voters: active members except initiator (and candidate).
        var eligible = db.ExpertGroupMembers
            .Where(m => m.ExpertGroupId == group.Id
                        && m.Status == ExpertMembershipStatus.Active
                        && m.UserId != invite.InvitedByUserId
                        && m.UserId != userId)
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        invite.EligibleVoterUserIdsCsv = string.Join(",", eligible);
        invite.RequiredApprovalCount = IExpertMembershipGovernanceService.RequiredApprovals(eligible.Count);
        invite.VoteOpenedAtUtc = DateTime.UtcNow;
        invite.VoteExpiresAtUtc = DateTime.UtcNow.AddDays(VoteDays);

        if (eligible.Count == 0)
        {
            // Admin must decide.
            invite.Status = ExpertMembershipInviteStatus.AwaitingAdminValidation;
            invite.RequiredApprovalCount = 0;
        }
        else if (eligible.Count == 1)
        {
            invite.Status = ExpertMembershipInviteStatus.PendingMemberApproval;
            invite.RequiredApprovalCount = 1;
        }
        else
        {
            invite.Status = ExpertMembershipInviteStatus.PendingMemberApproval;
        }

        invite.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await NotifyEligibleVotersAsync(invite, group.Name, ct);

        return await MapInviteAsync(invite, null, ct);
    }

    public async Task DeclineInviteAsync(string token, CancellationToken ct = default)
    {
        var invite = GetByToken(token);
        if (invite.Status is ExpertMembershipInviteStatus.Approved or ExpertMembershipInviteStatus.Rejected)
            return;
        invite.Status = ExpertMembershipInviteStatus.Cancelled;
        invite.DecisionAtUtc = DateTime.UtcNow;
        invite.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ExpertMembershipInviteDto> CastVoteAsync(
        string voterUserId,
        Guid inviteId,
        CastExpertMembershipVoteRequest request,
        CancellationToken ct = default)
    {
        var invite = db.ExpertMembershipInvites.FirstOrDefault(i => i.Id == inviteId)
            ?? throw new InvalidOperationException("Candidature introuvable.");
        ExpireIfNeeded(invite);

        if (invite.Status is not (ExpertMembershipInviteStatus.PendingMemberApproval
            or ExpertMembershipInviteStatus.AwaitingAdminValidation))
            throw new InvalidOperationException("Le vote est clos.");

        if (invite.VoteExpiresAtUtc is DateTime ve && ve < DateTime.UtcNow)
        {
            await RejectInviteAsync(invite, "Vote expiré.", ct);
            throw new InvalidOperationException("Le vote a expiré.");
        }

        var eligible = ParseEligible(invite.EligibleVoterUserIdsCsv);
        if (!eligible.Contains(voterUserId, StringComparer.Ordinal))
            throw new InvalidOperationException("Vous n'êtes pas autorisé à voter sur cette candidature.");

        if (voterUserId == invite.InvitedByUserId)
            throw new InvalidOperationException("L'initiateur ne peut pas voter.");

        var existing = db.ExpertMembershipVotes.FirstOrDefault(v => v.InviteId == inviteId && v.VoterUserId == voterUserId);
        if (existing is null)
        {
            db.Add(new ExpertMembershipVote
            {
                InviteId = inviteId,
                VoterUserId = voterUserId,
                Choice = request.Choice,
                Comment = TrimOrNull(request.Comment),
                VotedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.Choice = request.Choice;
            existing.Comment = TrimOrNull(request.Comment);
            existing.VotedAtUtc = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await EvaluateAfterVoteAsync(invite, ct);
        return await MapInviteAsync(invite, voterUserId, ct);
    }

    public async Task<ExpertMembershipInviteDto> AdminForceApproveAsync(
        string adminUserId, Guid inviteId, AdminExpertMembershipActionRequest? request, CancellationToken ct = default)
    {
        var invite = RequireInvite(inviteId);
        await AdmitAsync(invite, ExpertAdmissionMethod.AdminDirect, adminUserId, request?.Notes, ct);
        return await MapInviteAsync(invite, null, ct);
    }

    public async Task<ExpertMembershipInviteDto> AdminForceRejectAsync(
        string adminUserId, Guid inviteId, AdminExpertMembershipActionRequest? request, CancellationToken ct = default)
    {
        var invite = RequireInvite(inviteId);
        invite.AdminClosedByUserId = adminUserId;
        invite.AdminNotes = TrimOrNull(request?.Notes);
        await RejectInviteAsync(invite, request?.Notes ?? "Rejet administratif.", ct);
        return await MapInviteAsync(invite, null, ct);
    }

    public async Task<ExpertMembershipInviteDto> AdminCancelAsync(
        string adminUserId, Guid inviteId, AdminExpertMembershipActionRequest? request, CancellationToken ct = default)
    {
        var invite = RequireInvite(inviteId);
        invite.Status = ExpertMembershipInviteStatus.Cancelled;
        invite.AdminClosedByUserId = adminUserId;
        invite.AdminNotes = TrimOrNull(request?.Notes);
        invite.DecisionAtUtc = DateTime.UtcNow;
        invite.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await MapInviteAsync(invite, null, ct);
    }

    public async Task<ExpertMembershipInviteDto> AdminExtendAsync(
        string adminUserId, Guid inviteId, AdminExpertMembershipActionRequest request, CancellationToken ct = default)
    {
        var invite = RequireInvite(inviteId);
        if (request.ExtendInviteDays is int idays && idays > 0)
            invite.InviteExpiresAtUtc = invite.InviteExpiresAtUtc.AddDays(idays);
        if (request.ExtendVoteDays is int vdays && vdays > 0 && invite.VoteExpiresAtUtc is DateTime ve)
            invite.VoteExpiresAtUtc = ve.AddDays(vdays);
        invite.AdminClosedByUserId = adminUserId;
        invite.AdminNotes = TrimOrNull(request.Notes);
        invite.UpdatedAt = DateTime.UtcNow;
        if (invite.Status == ExpertMembershipInviteStatus.Expired)
            invite.Status = invite.VoteOpenedAtUtc is null
                ? ExpertMembershipInviteStatus.Sent
                : ExpertMembershipInviteStatus.PendingMemberApproval;
        await db.SaveChangesAsync(ct);
        return await MapInviteAsync(invite, null, ct);
    }

    public async Task<ExpertMembershipInviteDto> AdminValidateSmallGroupAsync(
        string adminUserId, Guid inviteId, AdminExpertMembershipActionRequest? request, CancellationToken ct = default)
    {
        var invite = RequireInvite(inviteId);
        if (invite.Status != ExpertMembershipInviteStatus.AwaitingAdminValidation)
            throw new InvalidOperationException("Cette candidature n'attend pas de validation admin.");
        await AdmitAsync(invite, ExpertAdmissionMethod.MemberVote, adminUserId, request?.Notes, ct);
        return await MapInviteAsync(invite, null, ct);
    }

    private async Task EvaluateAfterVoteAsync(ExpertMembershipInvite invite, CancellationToken ct)
    {
        var eligible = ParseEligible(invite.EligibleVoterUserIdsCsv);
        var votes = db.ExpertMembershipVotes.Where(v => v.InviteId == invite.Id).ToList();
        var approvals = votes.Count(v => v.Choice == ExpertMembershipVoteChoice.Approve);
        var rejects = votes.Count(v => v.Choice == ExpertMembershipVoteChoice.Reject);
        var required = invite.RequiredApprovalCount;

        // N=1: after approve → awaiting admin validation
        if (eligible.Count == 1 && approvals >= 1)
        {
            invite.Status = ExpertMembershipInviteStatus.AwaitingAdminValidation;
            invite.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        if (eligible.Count >= 2 && approvals >= required && required > 0)
        {
            await AdmitAsync(invite, ExpertAdmissionMethod.MemberVote, approvedByAdminId: null, notes: null, ct);
            return;
        }

        // Mathematical impossibility
        var remaining = eligible.Count - votes.Count;
        if (approvals + remaining < required)
        {
            await RejectInviteAsync(invite, "Seuil d'approbation mathématiquement impossible.", ct);
            return;
        }

        if (invite.VoteExpiresAtUtc is DateTime ve && ve < DateTime.UtcNow && approvals < required)
        {
            await RejectInviteAsync(invite, "Vote expiré sans atteindre le seuil.", ct);
        }
    }

    private async Task AdmitAsync(
        ExpertMembershipInvite invite,
        ExpertAdmissionMethod method,
        string? approvedByAdminId,
        string? notes,
        CancellationToken ct)
    {
        if (invite.Status is ExpertMembershipInviteStatus.Approved)
            return;

        var userId = invite.CandidateUserId
            ?? throw new InvalidOperationException("Le candidat n'a pas encore soumis son profil.");

        if (db.ExpertGroupMembers.Any(m =>
                m.UserId == userId
                && (m.Status == ExpertMembershipStatus.Active || m.Status == ExpertMembershipStatus.Suspended)
                && m.ExpertGroupId != invite.ExpertGroupId))
            throw new InvalidOperationException("Le candidat appartient déjà à un autre groupe.");

        var votes = db.ExpertMembershipVotes.Where(v => v.InviteId == invite.Id).ToList();
        var approvals = votes.Count(v => v.Choice == ExpertMembershipVoteChoice.Approve);

        var existing = db.ExpertGroupMembers.FirstOrDefault(m =>
            m.ExpertGroupId == invite.ExpertGroupId && m.UserId == userId);
        if (existing is null)
        {
            db.Add(new ExpertGroupMember
            {
                ExpertGroupId = invite.ExpertGroupId,
                UserId = userId,
                Status = ExpertMembershipStatus.Active,
                AdmissionMethod = method,
                Specialty = invite.Specialty,
                AdmittedAtUtc = DateTime.UtcNow,
                ApprovedByAdminId = approvedByAdminId,
                ApprovalCount = approvals,
                RequiredApprovalCount = invite.RequiredApprovalCount
            });
        }
        else
        {
            existing.Status = ExpertMembershipStatus.Active;
            existing.AdmissionMethod = method;
            existing.Specialty = invite.Specialty;
            existing.AdmittedAtUtc = DateTime.UtcNow;
            existing.ApprovedByAdminId = approvedByAdminId;
            existing.ApprovalCount = approvals;
            existing.RequiredApprovalCount = invite.RequiredApprovalCount;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        invite.Status = ExpertMembershipInviteStatus.Approved;
        invite.DecisionAtUtc = DateTime.UtcNow;
        invite.AdminClosedByUserId = approvedByAdminId;
        invite.AdminNotes = TrimOrNull(notes);
        invite.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await identity.EnsureExpertRoleAsync(userId, ct);
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == invite.ExpertGroupId);
        try
        {
            await identity.NotifyExpertAdmittedAsync(userId, group?.Name ?? "TutorSphere", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Échec notification admission {InviteId}", invite.Id);
        }
    }

    private async Task RejectInviteAsync(ExpertMembershipInvite invite, string reason, CancellationToken ct)
    {
        invite.Status = ExpertMembershipInviteStatus.Rejected;
        invite.DecisionAtUtc = DateTime.UtcNow;
        invite.AdminNotes = string.IsNullOrWhiteSpace(invite.AdminNotes) ? reason : invite.AdminNotes;
        invite.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        try
        {
            await email.SendExpertMembershipRejectedAsync(
                invite.Email, invite.FirstName, reason, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Échec e-mail rejet membership {InviteId}", invite.Id);
        }
    }

    private async Task NotifyEligibleVotersAsync(ExpertMembershipInvite invite, string groupName, CancellationToken ct)
    {
        var voteUrl = $"{urls.WebBaseUrl.TrimEnd('/')}/expert/admissions";
        foreach (var voterId in ParseEligible(invite.EligibleVoterUserIdsCsv))
        {
            var c = await contacts.GetAsync(voterId, ct);
            if (c is null || string.IsNullOrWhiteSpace(c.Value.Email)) continue;
            try
            {
                await email.SendExpertMembershipVoteOpenedAsync(
                    c.Value.Email,
                    c.Value.DisplayName,
                    $"{invite.FirstName} {invite.LastName}",
                    groupName,
                    voteUrl,
                    ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Échec notif vote à {Voter}", voterId);
            }
        }
    }

    private ExpertGroupMember RequireActiveMembership(string userId)
    {
        var m = db.ExpertGroupMembers.FirstOrDefault(x =>
            x.UserId == userId && x.Status == ExpertMembershipStatus.Active)
            ?? throw new InvalidOperationException("Vous n'êtes pas membre actif d'un groupe d'experts.");
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == m.ExpertGroupId && g.IsActive);
        if (group is null)
            throw new InvalidOperationException("Votre groupe d'experts est inactif.");
        return m;
    }

    private ExpertMembershipInvite RequireInvite(Guid id) =>
        db.ExpertMembershipInvites.FirstOrDefault(i => i.Id == id)
        ?? throw new InvalidOperationException("Candidature introuvable.");

    private ExpertMembershipInvite GetByToken(string token)
    {
        var t = (token ?? "").Trim();
        if (string.IsNullOrWhiteSpace(t))
            throw new InvalidOperationException("Jeton invalide.");
        return db.ExpertMembershipInvites.FirstOrDefault(i => i.Token == t)
            ?? throw new InvalidOperationException("Invitation introuvable.");
    }

    private void ExpireStaleInvites(Guid groupId)
    {
        var now = DateTime.UtcNow;
        var stale = db.ExpertMembershipInvites
            .Where(i => i.ExpertGroupId == groupId
                        && ((i.Status == ExpertMembershipInviteStatus.Sent && i.InviteExpiresAtUtc < now)
                            || (i.Status == ExpertMembershipInviteStatus.PendingMemberApproval
                                && i.VoteExpiresAtUtc != null && i.VoteExpiresAtUtc < now)))
            .ToList();
        foreach (var i in stale)
        {
            i.Status = ExpertMembershipInviteStatus.Expired;
            i.DecisionAtUtc = now;
            i.UpdatedAt = now;
        }
        if (stale.Count > 0)
            db.SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private void ExpireIfNeeded(ExpertMembershipInvite invite)
    {
        var now = DateTime.UtcNow;
        if (invite.Status == ExpertMembershipInviteStatus.Sent && invite.InviteExpiresAtUtc < now)
        {
            invite.Status = ExpertMembershipInviteStatus.Expired;
            invite.DecisionAtUtc = now;
        }
        else if (invite.Status == ExpertMembershipInviteStatus.PendingMemberApproval
                 && invite.VoteExpiresAtUtc is DateTime ve && ve < now)
        {
            invite.Status = ExpertMembershipInviteStatus.Expired;
            invite.DecisionAtUtc = now;
        }
    }

    private async Task<ExpertMembershipInviteDto> MapInviteAsync(
        ExpertMembershipInvite invite,
        string? viewerUserId,
        CancellationToken ct)
    {
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == invite.ExpertGroupId);
        var inviter = await contacts.GetAsync(invite.InvitedByUserId, ct);
        var votes = db.ExpertMembershipVotes.Where(v => v.InviteId == invite.Id).ToList();
        var voteDtos = new List<ExpertMembershipVoteDto>();
        foreach (var v in votes)
        {
            var voter = await contacts.GetAsync(v.VoterUserId, ct);
            voteDtos.Add(new ExpertMembershipVoteDto(
                v.VoterUserId, voter?.DisplayName, v.Choice, v.Comment, v.VotedAtUtc));
        }

        int? myVote = null;
        if (viewerUserId is not null)
        {
            var mine = votes.FirstOrDefault(v => v.VoterUserId == viewerUserId);
            if (mine is not null) myVote = (int)mine.Choice;
        }

        var eligibleCount = ParseEligible(invite.EligibleVoterUserIdsCsv).Count;

        return new ExpertMembershipInviteDto(
            invite.Id,
            invite.ExpertGroupId,
            group?.Name ?? "",
            invite.Email,
            invite.FirstName,
            invite.LastName,
            invite.Phone,
            invite.Specialty,
            invite.IntendedRole,
            invite.Presentation,
            invite.Justification,
            invite.InvitedByUserId,
            inviter?.DisplayName,
            invite.Status,
            invite.SentAtUtc,
            invite.InviteExpiresAtUtc,
            invite.VoteOpenedAtUtc,
            invite.VoteExpiresAtUtc,
            eligibleCount,
            invite.RequiredApprovalCount,
            votes.Count(v => v.Choice == ExpertMembershipVoteChoice.Approve),
            votes.Count(v => v.Choice == ExpertMembershipVoteChoice.Reject),
            votes.Count(v => v.Choice == ExpertMembershipVoteChoice.Abstain),
            myVote,
            voteDtos,
            invite.CandidateUserId,
            invite.DecisionAtUtc,
            invite.AdminNotes);
    }

    private static List<string> ParseEligible(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();

    private static string NormalizeEmail(string? email)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(e) || !e.Contains('@'))
            throw new InvalidOperationException("Adresse e-mail invalide.");
        return e;
    }

    private static string RequireText(string? value, string label)
    {
        var t = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(t))
            throw new InvalidOperationException($"{label} requis.");
        return t;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
