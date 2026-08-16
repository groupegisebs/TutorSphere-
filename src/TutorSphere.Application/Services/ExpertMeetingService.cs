using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertApproval;
using TutorSphere.Application.DTOs.Meetings;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IExpertMeetingService
{
    Task<IReadOnlyList<MeetingListItemDto>> ListAsync(string userId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default);
    Task<MeetingDetailDto> GetAsync(string userId, Guid meetingId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default);
    Task<MeetingDetailDto> CreateAsync(string userId, CreateMeetingRequest request, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default);
    Task CancelAsync(string userId, Guid meetingId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default);
    Task<MeetingCandidatePageDto> SearchCandidatesAsync(
        string userId, string? category, string? query, int page, int pageSize,
        MeetingVisibility visibility, IReadOnlyList<Guid>? groupIds,
        bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default);
    Task<IReadOnlyList<(Guid Id, string Name, string? Country)>> ListAccessibleGroupsAsync(
        string userId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default);
    Task StartAsync(string userId, Guid meetingId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default);
    Task EndForAllAsync(string userId, Guid meetingId, CancellationToken ct = default);
    Task AdmitAsync(string userId, Guid meetingId, Guid participantId, bool admit, CancellationToken ct = default);
    Task RespondAsync(string userId, Guid meetingId, MeetingParticipantStatus response, CancellationToken ct = default);
    Task SetParticipantRoleAsync(string userId, Guid meetingId, Guid participantId, MeetingParticipantRole role, CancellationToken ct = default);
    Task RemoveParticipantAsync(string userId, Guid meetingId, Guid participantId, CancellationToken ct = default);
    Task LockAsync(string userId, Guid meetingId, bool locked, CancellationToken ct = default);
    Task EnableAiAsync(string userId, Guid meetingId, CancellationToken ct = default);
    Task SetAiConsentAsync(string userId, Guid meetingId, string subjectKey, bool consented, CancellationToken ct = default);
    Task<MeetingMinutesDto> GetMinutesAsync(string userId, Guid meetingId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default);
    Task ReviewActionAsync(string userId, Guid meetingId, Guid actionId, ReviewActionItemRequest request, CancellationToken ct = default);
    Task ReviewDecisionAsync(string userId, Guid meetingId, Guid decisionId, bool accepted, CancellationToken ct = default);
    Task GenerateAiDraftAsync(string userId, Guid meetingId, CancellationToken ct = default);
    Task AppendTranscriptAsync(Guid meetingId, string chunk, CancellationToken ct = default);
    Task<GuestPreviewDto> PreviewGuestAsync(string token, CancellationToken ct = default);
    Task<GuestEnterResult> EnterGuestAsync(GuestEnterRequest request, CancellationToken ct = default);
    Task RevokeGuestAsync(string userId, Guid meetingId, Guid guestId, CancellationToken ct = default);
    Task ResendGuestAsync(string userId, Guid meetingId, Guid guestId, CancellationToken ct = default);
    Task<string> SetAccessCodeAsync(string userId, Guid meetingId, string? code, CancellationToken ct = default);
    Task EnsureCanJoinLiveAsync(string? userId, Guid meetingId, string? guestToken, string? accessCode = null, CancellationToken ct = default);
    Task PersistChatAsync(Guid meetingId, string senderUserId, string senderName, string body, CancellationToken ct = default);
    Task ToggleRecordingAsync(string userId, Guid meetingId, bool recording, CancellationToken ct = default);
    Task SetMinutesShareAsync(string userId, Guid meetingId, MeetingMinutesShare share, CancellationToken ct = default);
    Task ApproveMinutesAsync(string userId, Guid meetingId, CancellationToken ct = default);
    Task SendMinutesEmailAsync(string userId, Guid meetingId, IReadOnlyList<string>? extraEmails, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default);
    string BuildIcs(MeetingDetailDto meeting);
    Task ProcessRemindersAndRetriesAsync(CancellationToken ct = default);
    IReadOnlyList<string> PermissionsFor(string userId, Guid? groupId, bool asPlatformAdmin);
}

public class ExpertMeetingService(
    IApplicationDbContext db,
    IExpertGroupManagerService managers,
    IUserContactLookup contacts,
    IEmailService email,
    IAppUrlProvider urls,
    IExpertGovernanceAuditService audit) : IExpertMeetingService
{
    public IReadOnlyList<string> PermissionsFor(string userId, Guid? groupId, bool asPlatformAdmin)
    {
        if (asPlatformAdmin) return GroupMemberPermissionCatalog.All.Select(a => a.Key).ToList();
        if (groupId is Guid gid && managers.IsActiveManager(userId, gid))
            return GroupMemberPermissionCatalog.DefaultsFor(ExpertGroupMemberRole.Manager);
        if (groupId is Guid g2)
        {
            var member = db.ExpertGroupMembers.FirstOrDefault(m =>
                m.UserId == userId && m.ExpertGroupId == g2 && m.Status == ExpertMembershipStatus.Active);
            if (member is not null)
                return ReadMemberPermissions(member);
        }
        return [];
    }

    public async Task<IReadOnlyList<MeetingListItemDto>> ListAsync(
        string userId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default)
    {
        var scope = ResolveScope(userId, asPlatformAdmin, actAsGroupId);
        var q = db.Meetings.AsQueryable();
        if (!scope.Platform)
        {
            var groupIds = scope.GroupIds;
            q = q.Where(m => m.OrganizerUserId == userId
                || (m.OrganizerGroupId.HasValue && groupIds.Contains(m.OrganizerGroupId.Value))
                || db.MeetingGroups.Any(g => g.MeetingId == m.Id && groupIds.Contains(g.ExpertGroupId))
                || db.MeetingParticipants.Any(p => p.MeetingId == m.Id && p.UserId == userId));
        }

        var rows = q.OrderByDescending(m => m.StartAtUtc ?? m.CreatedAt).Take(200).ToList();
        var ids = rows.Select(m => m.Id).ToList();
        var parts = db.MeetingParticipants.Where(p => ids.Contains(p.MeetingId)).ToList();
        var groupNames = db.ExpertGroups.Select(g => new { g.Id, g.Name }).ToList()
            .ToDictionary(g => g.Id, g => g.Name);
        var links = db.MeetingGroups.Where(g => ids.Contains(g.MeetingId)).ToList();
        // Un même utilisateur revient sur beaucoup de réunions : une seule résolution par personne.
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        var result = new List<MeetingListItemDto>(rows.Count);
        foreach (var m in rows)
        {
            var mine = parts.Where(p => p.MeetingId == m.Id).ToList();
            var groupId = m.OrganizerGroupId ?? links.FirstOrDefault(l => l.MeetingId == m.Id)?.ExpertGroupId;
            var preview = new List<string>(4);
            foreach (var p in mine.Where(p => p.UserId is not null).Take(4))
                preview.Add(await ResolveNameAsync(p.UserId!, names, ct));

            result.Add(new MeetingListItemDto(
                m.Id, m.Title, m.Status, m.Visibility, m.StartAtUtc, m.EndAtUtc, m.TimeZoneId,
                await ResolveNameAsync(m.OrganizerUserId, names, ct),
                mine.Count,
                m.AiEnabled,
                m.OrganizerUserId == userId,
                groupId,
                groupId is Guid gid2 && groupNames.TryGetValue(gid2, out var gname) ? gname : null,
                mine.FirstOrDefault(p => p.UserId == userId)?.Status,
                preview));
        }
        return result;
    }

    private async Task<string> ResolveNameAsync(string userId, Dictionary<string, string> cache, CancellationToken ct)
    {
        if (cache.TryGetValue(userId, out var known)) return known;
        var c = await contacts.GetAsync(userId, ct);
        var name = c?.DisplayName ?? "Participant";
        cache[userId] = name;
        return name;
    }

    public async Task<MeetingDetailDto> GetAsync(
        string userId, Guid meetingId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default)
    {
        var meeting = RequireVisible(userId, meetingId, asPlatformAdmin, actAsGroupId);
        if (meeting.AccessCodeHash is null)
        {
            ApplyAccessCode(meeting, null);
            await db.SaveChangesAsync(ct);
        }
        return await MapDetailAsync(userId, meeting, asPlatformAdmin, ct);
    }

    public async Task<MeetingDetailDto> CreateAsync(
        string userId, CreateMeetingRequest request, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default)
    {
        var title = (request.Title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Le titre est obligatoire.");
        if ((request.Description ?? "").Length > 500)
            throw new InvalidOperationException("La description dépasse 500 caractères.");
        if ((request.Agenda ?? "").Length > 1000)
            throw new InvalidOperationException("L’ordre du jour dépasse 1 000 caractères.");

        var scope = ResolveScope(userId, asPlatformAdmin, actAsGroupId);
        EnsureCan(userId, scope.PrimaryGroupId, asPlatformAdmin, GroupMemberPermissionCatalog.MeetingsCreate);
        ValidateVisibility(request.Visibility, scope);

        var groups = NormalizeGroups(request.Visibility, request.GroupIds, scope);
        var internals = (request.InternalUserIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        EnsureCandidatesInScope(internals, groups, scope, request.Visibility);

        var mode = (request.SaveMode ?? "draft").Trim().ToLowerInvariant();
        var status = mode switch
        {
            "start" => MeetingStatus.Live,
            "schedule" => MeetingStatus.Scheduled,
            _ => MeetingStatus.Draft
        };
        var start = request.StartAtUtc;
        var end = request.EndAtUtc;
        if (mode == "start")
        {
            start ??= DateTime.UtcNow;
            end ??= start.Value.AddMinutes(45);
        }
        if (status != MeetingStatus.Draft && start is null)
            throw new InvalidOperationException("Indiquez la date et l’heure de début.");

        var meeting = new Meeting
        {
            OrganizerGroupId = scope.PrimaryGroupId,
            OrganizerUserId = userId,
            Title = title,
            Description = TrimOrNull(request.Description, 500),
            Agenda = TrimOrNull(request.Agenda, 1000),
            StartAtUtc = start,
            EndAtUtc = end,
            TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "Africa/Douala" : request.TimeZoneId.Trim(),
            Visibility = request.Visibility,
            Status = status,
            IsImmediate = mode == "start",
            WaitingRoomEnabled = request.WaitingRoomEnabled,
            AllowMic = request.AllowMic,
            AllowCamera = request.AllowCamera,
            AllowScreenShare = request.AllowScreenShare,
            RecordingEnabled = request.RecordingEnabled,
            TranscriptionEnabled = request.TranscriptionEnabled,
            AiEnabled = false,
            Language = string.IsNullOrWhiteSpace(request.Language) ? "fr" : request.Language.Trim(),
            Remind24h = request.Remind24h,
            Remind1h = request.Remind1h,
            Remind10m = request.Remind10m,
            SendEmailInvites = request.SendEmailInvites,
            LiveStartedAtUtc = status == MeetingStatus.Live ? DateTime.UtcNow : null
        };
        ApplyAccessCode(meeting, request.AccessCode);
        if (request.AiEnabled)
        {
            if (!HasPerm(userId, scope.PrimaryGroupId, asPlatformAdmin, GroupMemberPermissionCatalog.MeetingsEnableAi))
                throw new InvalidOperationException("Vous n’avez pas le droit d’activer l’assistant IA.");
            meeting.AiEnabled = true;
        }

        db.Add(meeting);
        foreach (var gid in groups)
            db.Add(new MeetingGroup { MeetingId = meeting.Id, ExpertGroupId = gid });

        if (request.Recurring && request.RecurrenceFrequency != MeetingRecurrenceFrequency.None)
        {
            db.Add(new MeetingRecurrence
            {
                MeetingId = meeting.Id,
                Frequency = request.RecurrenceFrequency,
                Interval = 1
            });
        }

        db.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = userId,
            Role = MeetingParticipantRole.Organizer,
            Status = MeetingParticipantStatus.Accepted
        });

        foreach (var uid in internals.Where(id => !string.Equals(id, userId, StringComparison.Ordinal)))
        {
            db.Add(new MeetingParticipant
            {
                MeetingId = meeting.Id,
                UserId = uid,
                Role = MeetingParticipantRole.Participant,
                Status = MeetingParticipantStatus.Invited
            });
        }

        var guestPlain = new List<(MeetingExternalGuest Guest, string Token)>();
        if (request.ExternalGuests is { Count: > 0 })
        {
            EnsureCan(userId, scope.PrimaryGroupId, asPlatformAdmin, GroupMemberPermissionCatalog.MeetingsInviteExternal);
            foreach (var g in request.ExternalGuests.Where(x => !string.IsNullOrWhiteSpace(x.Email)))
            {
                var token = NewToken();
                var guest = new MeetingExternalGuest
                {
                    MeetingId = meeting.Id,
                    FullName = string.IsNullOrWhiteSpace(g.FullName) ? g.Email.Trim() : g.FullName.Trim(),
                    Email = g.Email.Trim(),
                    TokenHash = Hash(token),
                    AccessCode = GenerateAccessCode(),
                    TokenExpiresAtUtc = (start ?? DateTime.UtcNow).AddHours(36)
                };
                db.Add(guest);
                db.Add(new MeetingParticipant
                {
                    MeetingId = meeting.Id,
                    ExternalGuestId = guest.Id,
                    Role = MeetingParticipantRole.ExternalGuest,
                    Status = MeetingParticipantStatus.Invited
                });
                guestPlain.Add((guest, token));
            }
        }

        await db.SaveChangesAsync(ct);
        Audit(meeting.Id, userId, "created", title);

        if (status != MeetingStatus.Draft && request.SendEmailInvites)
            await SendInvitesAsync(meeting, guestPlain, ct);

        await audit.RecordAsync(
            ExpertGovernanceEventType.MeetingCreated,
            userId,
            $"Réunion « {title} » {(status == MeetingStatus.Live ? "démarrée" : status == MeetingStatus.Scheduled ? "programmée" : "enregistrée en brouillon")}.",
            meeting.OrganizerGroupId, relatedEntityId: meeting.Id, isNotification: true, ct: ct);

        return await MapDetailAsync(userId, meeting, asPlatformAdmin, ct);
    }

    public async Task CancelAsync(string userId, Guid meetingId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default)
    {
        var meeting = RequireVisible(userId, meetingId, asPlatformAdmin, actAsGroupId);
        EnsureCan(userId, meeting.OrganizerGroupId, asPlatformAdmin, GroupMemberPermissionCatalog.MeetingsCancel);
        if (meeting.OrganizerUserId != userId && !asPlatformAdmin
            && !HasPerm(userId, meeting.OrganizerGroupId, asPlatformAdmin, GroupMemberPermissionCatalog.MeetingsUpdateGroup))
            throw new InvalidOperationException("Vous ne pouvez pas annuler cette réunion.");
        meeting.Status = MeetingStatus.Cancelled;
        meeting.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        Audit(meeting.Id, userId, "cancelled", meeting.Title);
        foreach (var inv in db.MeetingInvitations.Where(i => i.MeetingId == meeting.Id).ToList())
        {
            try
            {
                await email.SendMeetingCancelledAsync(inv.RecipientEmail, meeting.Title, meeting.StartAtUtc ?? DateTime.UtcNow, ct);
            }
            catch { /* logged by email */ }
        }
        await audit.RecordAsync(ExpertGovernanceEventType.MeetingCancelled, userId,
            $"Réunion « {meeting.Title} » annulée.", meeting.OrganizerGroupId, relatedEntityId: meeting.Id, isNotification: true, ct: ct);
    }

    public Task<IReadOnlyList<(Guid Id, string Name, string? Country)>> ListAccessibleGroupsAsync(
        string userId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default)
    {
        var scope = ResolveScope(userId, asPlatformAdmin, actAsGroupId);
        IEnumerable<ExpertGroup> groups = asPlatformAdmin || scope.Platform
            ? db.ExpertGroups.Where(g => g.IsActive).OrderBy(g => g.Name).ToList()
            : db.ExpertGroups.Where(g => scope.GroupIds.Contains(g.Id)).OrderBy(g => g.Name).ToList();
        IReadOnlyList<(Guid, string, string?)> list = groups.Select(g => (g.Id, g.Name, g.CountryCode)).ToList();
        return Task.FromResult(list);
    }

    public async Task<MeetingCandidatePageDto> SearchCandidatesAsync(
        string userId, string? category, string? query, int page, int pageSize,
        MeetingVisibility visibility, IReadOnlyList<Guid>? groupIds,
        bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default)
    {
        var scope = ResolveScope(userId, asPlatformAdmin, actAsGroupId);
        ValidateVisibility(visibility, scope);
        var allowed = NormalizeGroups(visibility, groupIds, scope);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 40);
        var q = (query ?? "").Trim();
        var cat = (category ?? "members").Trim().ToLowerInvariant();

        if (cat is "external" or "invites")
            return new MeetingCandidatePageDto([], 0, page, pageSize);

        if (cat == "groups")
        {
            var groups = db.ExpertGroups.Where(g => allowed.Contains(g.Id)).AsEnumerable();
            if (!string.IsNullOrWhiteSpace(q))
                groups = groups.Where(g => g.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
            var allG = groups.OrderBy(g => g.Name).ToList();
            var slice = allG.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(g => new MeetingCandidateDto(
                    $"g:{g.Id}", "group", null, g.Id, g.Name, null, "Groupe", g.Name, g.CountryCode, null))
                .ToList();
            return new MeetingCandidatePageDto(slice, allG.Count, page, pageSize);
        }

        var members = db.ExpertGroupMembers
            .Where(m => allowed.Contains(m.ExpertGroupId) && m.Status == ExpertMembershipStatus.Active)
            .ToList();
        if (cat == "admins")
            members = members.Where(m => m.MemberRole == ExpertGroupMemberRole.Manager).ToList();
        else if (cat == "experts")
            members = members.Where(m => m.MemberRole != ExpertGroupMemberRole.Manager).ToList();

        var items = new List<MeetingCandidateDto>();
        foreach (var m in members)
        {
            var c = await contacts.GetAsync(m.UserId, ct);
            var name = c?.DisplayName ?? m.UserId;
            var mail = c?.Email ?? "";
            if (!string.IsNullOrWhiteSpace(q)
                && name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                && mail.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            var group = db.ExpertGroups.FirstOrDefault(g => g.Id == m.ExpertGroupId);
            items.Add(new MeetingCandidateDto(
                m.UserId, cat == "admins" ? "admin" : "member", m.UserId, m.ExpertGroupId,
                name, string.IsNullOrWhiteSpace(mail) ? null : mail,
                RoleFr(m.MemberRole), group?.Name, group?.CountryCode, null));
        }

        var distinct = items.GroupBy(i => i.UserId, StringComparer.Ordinal).Select(g => g.First())
            .OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        var total = distinct.Count;
        var pageItems = distinct.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new MeetingCandidatePageDto(pageItems, total, page, pageSize);
    }

    public async Task StartAsync(string userId, Guid meetingId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default)
    {
        var meeting = RequireVisible(userId, meetingId, asPlatformAdmin, actAsGroupId);
        if (meeting.OrganizerUserId != userId && !HasPerm(userId, meeting.OrganizerGroupId, asPlatformAdmin, GroupMemberPermissionCatalog.MeetingsModerate))
            throw new InvalidOperationException("Seul l’organisateur peut démarrer la réunion.");
        meeting.Status = MeetingStatus.Live;
        meeting.LiveStartedAtUtc ??= DateTime.UtcNow;
        meeting.UpdatedAt = DateTime.UtcNow;
        db.Add(new MeetingSession { MeetingId = meeting.Id, StartedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        Audit(meeting.Id, userId, "started", null);
        await audit.RecordAsync(ExpertGovernanceEventType.MeetingStarted, userId,
            $"Réunion « {meeting.Title} » en cours.", meeting.OrganizerGroupId, relatedEntityId: meeting.Id, isNotification: true, ct: ct);
    }

    public async Task EndForAllAsync(string userId, Guid meetingId, CancellationToken ct = default)
    {
        var meeting = db.Meetings.FirstOrDefault(m => m.Id == meetingId)
            ?? throw new InvalidOperationException("Réunion introuvable.");
        if (meeting.OrganizerUserId != userId
            && !HasPerm(userId, meeting.OrganizerGroupId, false, GroupMemberPermissionCatalog.MeetingsModerate))
            throw new InvalidOperationException("Vous ne pouvez pas terminer cette réunion.");
        meeting.Status = MeetingStatus.Ended;
        meeting.EndedAtUtc = DateTime.UtcNow;
        meeting.UpdatedAt = DateTime.UtcNow;
        var live = db.MeetingSessions.Where(s => s.MeetingId == meetingId && s.EndedAtUtc == null).ToList();
        foreach (var s in live) s.EndedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        Audit(meeting.Id, userId, "ended", null);
        if (meeting.AiActivatedByOrganizer)
            SeedAiDraft(meeting);
        await db.SaveChangesAsync(ct);
    }

    public async Task AdmitAsync(string userId, Guid meetingId, Guid participantId, bool admit, CancellationToken ct = default)
    {
        RequireModerator(userId, meetingId);
        var p = db.MeetingParticipants.FirstOrDefault(x => x.Id == participantId && x.MeetingId == meetingId)
            ?? throw new InvalidOperationException("Participant introuvable.");
        p.Status = admit ? MeetingParticipantStatus.Accepted : MeetingParticipantStatus.Denied;
        await db.SaveChangesAsync(ct);
        Audit(meetingId, userId, admit ? "admitted" : "denied", participantId.ToString());
    }

    /// <summary>Réponse de l'invité à sa convocation : accepte, hésite ou décline.</summary>
    public async Task RespondAsync(string userId, Guid meetingId, MeetingParticipantStatus response, CancellationToken ct = default)
    {
        if (response is not (MeetingParticipantStatus.Accepted
            or MeetingParticipantStatus.Tentative or MeetingParticipantStatus.Declined))
            throw new InvalidOperationException("Réponse non prise en charge.");
        var meeting = db.Meetings.FirstOrDefault(m => m.Id == meetingId)
            ?? throw new InvalidOperationException("Réunion introuvable.");
        if (meeting.Status is MeetingStatus.Cancelled or MeetingStatus.Ended)
            throw new InvalidOperationException("Cette réunion est clôturée.");
        var p = db.MeetingParticipants.FirstOrDefault(x => x.MeetingId == meetingId && x.UserId == userId)
            ?? throw new InvalidOperationException("Vous n’êtes pas invité à cette réunion.");
        if (p.Status is MeetingParticipantStatus.Removed or MeetingParticipantStatus.Denied)
            throw new InvalidOperationException("Votre participation a été retirée.");
        p.Status = response;
        await db.SaveChangesAsync(ct);
        Audit(meetingId, userId, "response", response.ToString());
    }

    public async Task SetParticipantRoleAsync(string userId, Guid meetingId, Guid participantId, MeetingParticipantRole role, CancellationToken ct = default)
    {
        RequireModerator(userId, meetingId);
        if (role == MeetingParticipantRole.Organizer)
            throw new InvalidOperationException("Utilisez le transfert d’organisation dédié.");
        var p = db.MeetingParticipants.FirstOrDefault(x => x.Id == participantId && x.MeetingId == meetingId)
            ?? throw new InvalidOperationException("Participant introuvable.");
        p.Role = role;
        await db.SaveChangesAsync(ct);
        Audit(meetingId, userId, "role", role.ToString());
    }

    public async Task RemoveParticipantAsync(string userId, Guid meetingId, Guid participantId, CancellationToken ct = default)
    {
        RequireModerator(userId, meetingId);
        var p = db.MeetingParticipants.FirstOrDefault(x => x.Id == participantId && x.MeetingId == meetingId)
            ?? throw new InvalidOperationException("Participant introuvable.");
        p.Status = MeetingParticipantStatus.Removed;
        await db.SaveChangesAsync(ct);
        Audit(meetingId, userId, "removed", participantId.ToString());
    }

    public async Task LockAsync(string userId, Guid meetingId, bool locked, CancellationToken ct = default)
    {
        RequireModerator(userId, meetingId);
        var meeting = db.Meetings.First(m => m.Id == meetingId);
        meeting.Locked = locked;
        await db.SaveChangesAsync(ct);
        Audit(meetingId, userId, locked ? "locked" : "unlocked", null);
    }

    public async Task EnableAiAsync(string userId, Guid meetingId, CancellationToken ct = default)
    {
        var meeting = db.Meetings.FirstOrDefault(m => m.Id == meetingId)
            ?? throw new InvalidOperationException("Réunion introuvable.");
        if (meeting.OrganizerUserId != userId)
            throw new InvalidOperationException("Seul l’organisateur peut activer l’assistant IA.");
        EnsureCan(userId, meeting.OrganizerGroupId, false, GroupMemberPermissionCatalog.MeetingsEnableAi);
        meeting.AiEnabled = true;
        meeting.AiActivatedByOrganizer = true;
        meeting.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        Audit(meetingId, userId, "ai-enabled", "Consentement requis pour chaque participant.");
    }

    public async Task SetAiConsentAsync(string userId, Guid meetingId, string subjectKey, bool consented, CancellationToken ct = default)
    {
        var meeting = db.Meetings.FirstOrDefault(m => m.Id == meetingId)
            ?? throw new InvalidOperationException("Réunion introuvable.");
        var key = string.IsNullOrWhiteSpace(subjectKey) ? userId : subjectKey;
        var row = db.MeetingAiConsents.FirstOrDefault(c => c.MeetingId == meetingId && c.SubjectKey == key);
        if (row is null)
        {
            row = new MeetingAIConsent { MeetingId = meetingId, SubjectKey = key };
            db.Add(row);
        }
        row.Consented = consented;
        row.RespondedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task GenerateAiDraftAsync(string userId, Guid meetingId, CancellationToken ct = default)
    {
        var meeting = RequireVisible(userId, meetingId, false, null);
        if (!meeting.AiActivatedByOrganizer)
            throw new InvalidOperationException("L’assistant IA n’est pas activé.");
        SeedAiDraft(meeting);
        await db.SaveChangesAsync(ct);
    }

    public async Task AppendTranscriptAsync(Guid meetingId, string chunk, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chunk)) return;
        var meeting = db.Meetings.FirstOrDefault(m => m.Id == meetingId) ?? throw new InvalidOperationException("Réunion introuvable.");
        var t = db.MeetingTranscripts.FirstOrDefault(x => x.MeetingId == meetingId);
        if (t is null)
        {
            t = new MeetingTranscript { MeetingId = meetingId, Language = meeting.Language };
            db.Add(t);
        }
        t.Content = (t.Content + "\n" + chunk.Trim()).Trim();
        if (t.Content.Length > 200_000) t.Content = t.Content[^180_000..];
        await db.SaveChangesAsync(ct);
    }

    public async Task<MeetingMinutesDto> GetMinutesAsync(
        string userId, Guid meetingId, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default)
    {
        var meeting = RequireVisible(userId, meetingId, asPlatformAdmin, actAsGroupId);
        EnsureCan(userId, meeting.OrganizerGroupId, asPlatformAdmin, GroupMemberPermissionCatalog.MeetingsViewTranscript);
        var detail = await MapDetailAsync(userId, meeting, asPlatformAdmin, ct);
        var summary = db.MeetingAiSummaries.Where(s => s.MeetingId == meetingId).OrderByDescending(s => s.CreatedAt).FirstOrDefault();
        var decisions = db.MeetingDecisions.Where(d => d.MeetingId == meetingId).ToList()
            .Select(d => new MeetingDecisionDto(d.Id, d.Text, d.FromAi, d.Accepted)).ToList();
        var actions = db.MeetingActionItems.Where(a => a.MeetingId == meetingId).ToList()
            .Select(a => new MeetingActionItemDto(a.Id, a.Title, a.AssigneeUserId, a.AssigneeName, a.DueAtUtc, a.Status, a.FromAi)).ToList();
        var chat = db.MeetingSessions.Where(s => s.MeetingId == meetingId).Select(s => s.Id).ToList();
        var messages = db.MeetingMessages.Where(m => chat.Contains(m.SessionId)).OrderBy(m => m.CreatedAt)
            .Select(m => $"{m.SenderName}: {m.Body}").ToList();
        var files = db.MeetingFiles.Where(f => f.MeetingId == meetingId).Select(f => f.FileName).ToList();
        var recording = db.MeetingRecordings.Any(r => r.MeetingId == meetingId);
        var transcript = db.MeetingTranscripts.Where(t => t.MeetingId == meetingId).Select(t => t.Content).FirstOrDefault();
        return new MeetingMinutesDto(
            detail,
            summary is null ? null : new MeetingAISummaryDto(
                summary.Id, summary.Overview, ReadJsonList(summary.TopicsJson),
                ReadJsonList(summary.OpenQuestionsJson), ReadJsonList(summary.RisksJson),
                summary.NextSteps, summary.IsDraft),
            decisions, actions, detail.Participants, messages, files, recording, transcript);
    }

    public async Task ReviewActionAsync(string userId, Guid meetingId, Guid actionId, ReviewActionItemRequest request, CancellationToken ct = default)
    {
        RequireModerator(userId, meetingId);
        var item = db.MeetingActionItems.FirstOrDefault(a => a.Id == actionId && a.MeetingId == meetingId)
            ?? throw new InvalidOperationException("Action introuvable.");
        item.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.Title)) item.Title = request.Title.Trim();
        item.AssigneeUserId = request.AssigneeUserId;
        item.AssigneeName = request.AssigneeName;
        item.DueAtUtc = request.DueAtUtc;
        await db.SaveChangesAsync(ct);
        Audit(meetingId, userId, "action-reviewed", item.Status.ToString());
    }

    public async Task ReviewDecisionAsync(string userId, Guid meetingId, Guid decisionId, bool accepted, CancellationToken ct = default)
    {
        RequireModerator(userId, meetingId);
        var item = db.MeetingDecisions.FirstOrDefault(d => d.Id == decisionId && d.MeetingId == meetingId)
            ?? throw new InvalidOperationException("Décision introuvable.");
        item.Accepted = accepted;
        await db.SaveChangesAsync(ct);
    }

    public Task<GuestPreviewDto> PreviewGuestAsync(string token, CancellationToken ct = default)
    {
        var guest = FindGuestByToken(token);
        var meeting = db.Meetings.First(m => m.Id == guest.MeetingId);
        var org = contacts.GetAsync(meeting.OrganizerUserId, ct).GetAwaiter().GetResult();
        // L'invité saisit toujours un code : le sien, reçu dans son invitation.
        return Task.FromResult(new GuestPreviewDto(
            meeting.Id, meeting.Title, meeting.StartAtUtc, org?.DisplayName ?? "Organisateur",
            true, meeting.WaitingRoomEnabled,
            meeting.RecordingEnabled, meeting.AiActivatedByOrganizer));
    }

    public async Task<GuestEnterResult> EnterGuestAsync(GuestEnterRequest request, CancellationToken ct = default)
    {
        var guest = FindGuestByToken(request.Token);
        if (guest.RevokedAtUtc is not null)
            throw new InvalidOperationException("Cette invitation a été révoquée.");
        if (guest.TokenExpiresAtUtc < DateTime.UtcNow)
            throw new InvalidOperationException("Ce lien a expiré.");
        var meeting = db.Meetings.First(m => m.Id == guest.MeetingId);
        if (meeting.Locked)
            throw new InvalidOperationException("La réunion est verrouillée.");
        // Invitations émises avant les codes personnels : on en crée un et on l'envoie une fois.
        if (string.IsNullOrWhiteSpace(guest.AccessCode))
        {
            var issued = GenerateAccessCode();
            guest.AccessCode = issued;
            await db.SaveChangesAsync(ct);
            await email.SendMeetingGuestCodeAsync(guest.Email, guest.FullName, meeting.Title, issued, ct);
            throw new InvalidOperationException("Un code de vérification a été envoyé à votre courriel.");
        }

        var provided = NormalizeAccessCode(request.AccessCode ?? request.EmailCode);
        if (provided is null || !SlowEquals(Hash(guest.AccessCode), Hash(provided)))
            throw new InvalidOperationException("Code d’invitation incorrect.");
        guest.VerifiedAtUtc ??= DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            guest.FullName = request.DisplayName.Trim();
        var p = db.MeetingParticipants.FirstOrDefault(x => x.MeetingId == meeting.Id && x.ExternalGuestId == guest.Id);
        if (p is not null)
            p.Status = meeting.WaitingRoomEnabled ? MeetingParticipantStatus.Waiting : MeetingParticipantStatus.Accepted;
        await db.SaveChangesAsync(ct);
        return new GuestEnterResult(meeting.Id, guest.FullName, meeting.WaitingRoomEnabled);
    }

    public async Task RevokeGuestAsync(string userId, Guid meetingId, Guid guestId, CancellationToken ct = default)
    {
        RequireModerator(userId, meetingId);
        var guest = db.MeetingExternalGuests.FirstOrDefault(g => g.Id == guestId && g.MeetingId == meetingId)
            ?? throw new InvalidOperationException("Invité introuvable.");
        guest.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ResendGuestAsync(string userId, Guid meetingId, Guid guestId, CancellationToken ct = default)
    {
        RequireModerator(userId, meetingId);
        var meeting = db.Meetings.First(m => m.Id == meetingId);
        var guest = db.MeetingExternalGuests.FirstOrDefault(g => g.Id == guestId && g.MeetingId == meetingId)
            ?? throw new InvalidOperationException("Invité introuvable.");
        // Nouveau lien = nouveau code personnel : l'ancien couple ne doit plus ouvrir la salle.
        var token = NewToken();
        guest.TokenHash = Hash(token);
        guest.AccessCode = GenerateAccessCode();
        guest.VerifiedAtUtc = null;
        guest.TokenExpiresAtUtc = DateTime.UtcNow.AddHours(36);
        guest.RevokedAtUtc = null;
        await db.SaveChangesAsync(ct);
        var org = await contacts.GetAsync(meeting.OrganizerUserId, ct);
        var join = GuestJoinUrl(guest, token);
        await email.SendMeetingInvitationAsync(
            guest.Email, guest.FullName, meeting.Title, meeting.StartAtUtc ?? DateTime.UtcNow,
            meeting.TimeZoneId, org?.DisplayName ?? "Organisateur", meeting.Agenda, join,
            meeting.RecordingEnabled, meeting.AiEnabled, true, guest.TokenExpiresAtUtc, guest.AccessCode, ct);
    }

    public async Task<string> SetAccessCodeAsync(string userId, Guid meetingId, string? code, CancellationToken ct = default)
    {
        var meeting = RequireModerator(userId, meetingId);
        var applied = ApplyAccessCode(meeting, code);
        meeting.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        Audit(meetingId, userId, "access-code", string.IsNullOrWhiteSpace(code) ? "regenerated" : "set");
        return applied;
    }

    public Task EnsureCanJoinLiveAsync(
        string? userId, Guid meetingId, string? guestToken, string? accessCode = null, CancellationToken ct = default)
    {
        var meeting = db.Meetings.FirstOrDefault(m => m.Id == meetingId)
            ?? throw new InvalidOperationException("Réunion introuvable.");
        if (meeting.Status is MeetingStatus.Cancelled)
            throw new InvalidOperationException("Cette réunion a été annulée.");
        if (!string.IsNullOrWhiteSpace(guestToken))
        {
            var guest = FindGuestByToken(guestToken);
            if (guest.MeetingId != meetingId || guest.RevokedAtUtc is not null)
                throw new InvalidOperationException("Invitation externe invalide.");
            // Le code personnel de l'invité est contrôlé sur la page d'accès :
            // sans vérification aboutie, le lien seul ne suffit pas pour entrer.
            if (guest.VerifiedAtUtc is null)
                throw new InvalidOperationException("Vérifiez votre invitation avant d’entrer.");
            return Task.CompletedTask;
        }
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("Authentification requise.");
        var allowed = meeting.OrganizerUserId == userId
            || db.MeetingParticipants.Any(p => p.MeetingId == meetingId && p.UserId == userId
                && p.Status != MeetingParticipantStatus.Denied && p.Status != MeetingParticipantStatus.Removed);
        if (!allowed)
            throw new InvalidOperationException("Vous n’êtes pas invité à cette réunion.");
        EnsureAccessCodeMatches(meeting, accessCode);
        return Task.CompletedTask;
    }

    public async Task PersistChatAsync(Guid meetingId, string senderUserId, string senderName, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body)) return;
        var session = db.MeetingSessions.Where(s => s.MeetingId == meetingId && s.EndedAtUtc == null)
            .OrderByDescending(s => s.StartedAtUtc).FirstOrDefault();
        if (session is null)
        {
            session = new MeetingSession { MeetingId = meetingId, StartedAtUtc = DateTime.UtcNow };
            db.Add(session);
            await db.SaveChangesAsync(ct);
        }
        var trimmed = body.Trim();
        db.Add(new MeetingMessage
        {
            SessionId = session.Id,
            SenderUserId = senderUserId,
            SenderName = string.IsNullOrWhiteSpace(senderName) ? "Participant" : senderName.Trim(),
            Body = trimmed.Length > 4000 ? trimmed[..4000] : trimmed
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task ToggleRecordingAsync(string userId, Guid meetingId, bool recording, CancellationToken ct = default)
    {
        var meeting = RequireModerator(userId, meetingId);
        EnsureCan(userId, meeting.OrganizerGroupId, false, GroupMemberPermissionCatalog.MeetingsRecord);
        meeting.RecordingEnabled = true;
        var active = db.MeetingRecordings.FirstOrDefault(r => r.MeetingId == meetingId && r.IsActive);
        if (recording)
        {
            if (active is null)
                db.Add(new MeetingRecording { MeetingId = meetingId, StartedAtUtc = DateTime.UtcNow, IsActive = true });
        }
        else if (active is not null)
        {
            active.IsActive = false;
            active.EndedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        Audit(meetingId, userId, recording ? "recording-on" : "recording-off", null);
    }

    public async Task SetMinutesShareAsync(string userId, Guid meetingId, MeetingMinutesShare share, CancellationToken ct = default)
    {
        var meeting = RequireVisible(userId, meetingId, false, null);
        EnsureCan(userId, meeting.OrganizerGroupId, false, GroupMemberPermissionCatalog.MeetingsManageMinutes);
        meeting.MinutesShare = share;
        await db.SaveChangesAsync(ct);
    }

    public async Task ApproveMinutesAsync(string userId, Guid meetingId, CancellationToken ct = default)
    {
        var meeting = RequireVisible(userId, meetingId, false, null);
        EnsureCan(userId, meeting.OrganizerGroupId, false, GroupMemberPermissionCatalog.MeetingsManageMinutes);
        meeting.MinutesApproved = true;
        var summary = db.MeetingAiSummaries.Where(s => s.MeetingId == meetingId).OrderByDescending(s => s.CreatedAt).FirstOrDefault();
        if (summary is not null) summary.IsDraft = false;
        await db.SaveChangesAsync(ct);
        Audit(meetingId, userId, "minutes-approved", null);
    }

    public async Task SendMinutesEmailAsync(
        string userId, Guid meetingId, IReadOnlyList<string>? extraEmails, bool asPlatformAdmin, Guid? actAsGroupId, CancellationToken ct = default)
    {
        var minutes = await GetMinutesAsync(userId, meetingId, asPlatformAdmin, actAsGroupId, ct);
        var url = $"{urls.WebBaseUrl.TrimEnd('/')}/expert/meetings/{meetingId}/minutes";
        var sent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in minutes.Attendance.Where(p => !string.IsNullOrWhiteSpace(p.Email)))
        {
            if (!sent.Add(p.Email!)) continue;
            await email.SendMeetingMinutesAsync(p.Email!, p.DisplayName, minutes.Meeting.Title, url, ct);
        }
        foreach (var extra in extraEmails ?? [])
        {
            var addr = extra.Trim();
            if (string.IsNullOrWhiteSpace(addr) || !sent.Add(addr)) continue;
            await email.SendMeetingMinutesAsync(addr, addr, minutes.Meeting.Title, url, ct);
        }
        db.Add(new MeetingNotification
        {
            MeetingId = meetingId,
            Kind = MeetingNotificationKind.Minutes,
            RecipientUserId = userId,
            RecipientEmail = "",
            SentAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public string BuildIcs(MeetingDetailDto meeting)
    {
        var start = meeting.StartAtUtc ?? DateTime.UtcNow;
        var end = meeting.EndAtUtc ?? start.AddMinutes(45);
        static string Stamp(DateTime d) => d.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");
        var desc = (meeting.Agenda ?? meeting.Description ?? "").Replace("\r", "").Replace("\n", "\\n");
        return $"""
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//TutorSphere//Meetings//FR
            BEGIN:VEVENT
            UID:{meeting.Id}@tutorsphere
            DTSTAMP:{Stamp(DateTime.UtcNow)}
            DTSTART:{Stamp(start)}
            DTEND:{Stamp(end)}
            SUMMARY:{meeting.Title.Replace(",", "\\,")}
            DESCRIPTION:{desc}
            END:VEVENT
            END:VCALENDAR
            """;
    }

    public async Task ProcessRemindersAndRetriesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var scheduled = db.Meetings.Where(m => m.Status == MeetingStatus.Scheduled && m.StartAtUtc != null).ToList();
        foreach (var meeting in scheduled)
        {
            var start = meeting.StartAtUtc!.Value;
            await MaybeRemind(meeting, MeetingNotificationKind.Reminder24h, meeting.Remind24h, start.AddHours(-24), TimeSpan.FromHours(1), now, ct);
            await MaybeRemind(meeting, MeetingNotificationKind.Reminder1h, meeting.Remind1h, start.AddHours(-1), TimeSpan.FromMinutes(20), now, ct);
            await MaybeRemind(meeting, MeetingNotificationKind.Reminder10m, meeting.Remind10m, start.AddMinutes(-10), TimeSpan.FromMinutes(8), now, ct);
        }

        var failed = db.MeetingInvitations
            .Where(i => i.Status == MeetingInvitationStatus.Failed && i.AttemptCount < 3)
            .ToList();
        var web = urls.WebBaseUrl.TrimEnd('/');
        foreach (var inv in failed)
        {
            var meeting = db.Meetings.FirstOrDefault(m => m.Id == inv.MeetingId);
            if (meeting is null || meeting.Status is MeetingStatus.Cancelled or MeetingStatus.Ended or MeetingStatus.Draft)
                continue;
            var org = await contacts.GetAsync(meeting.OrganizerUserId, ct);
            var organizer = org?.DisplayName ?? "Organisateur";
            try
            {
                if (inv.Kind == MeetingInvitationKind.External)
                {
                    // Un invité externe n'a pas de compte : il faut un nouveau lien signé, pas la salle interne.
                    var guest = inv.ExternalGuestId is Guid guestId
                        ? db.MeetingExternalGuests.FirstOrDefault(g => g.Id == guestId)
                        : db.MeetingExternalGuests.FirstOrDefault(g =>
                            g.MeetingId == meeting.Id && g.Email == inv.RecipientEmail);
                    if (guest is null)
                    {
                        inv.LastError = "Invité externe introuvable.";
                        inv.AttemptCount++;
                        inv.LastAttemptAtUtc = DateTime.UtcNow;
                        continue;
                    }

                    var token = NewToken();
                    guest.TokenHash = Hash(token);
                    guest.AccessCode = GenerateAccessCode();
                    guest.VerifiedAtUtc = null;
                    guest.TokenExpiresAtUtc = DateTime.UtcNow.AddHours(36);
                    guest.RevokedAtUtc = null;
                    await email.SendMeetingInvitationAsync(
                        guest.Email, guest.FullName, meeting.Title, meeting.StartAtUtc ?? DateTime.UtcNow,
                        meeting.TimeZoneId, organizer, meeting.Agenda,
                        GuestJoinUrl(guest, token),
                        meeting.RecordingEnabled, meeting.AiEnabled, true, guest.TokenExpiresAtUtc, guest.AccessCode, ct);
                }
                else
                {
                    var contact = inv.RecipientUserId is null
                        ? null
                        : await contacts.GetAsync(inv.RecipientUserId, ct);
                    await email.SendMeetingInvitationAsync(
                        inv.RecipientEmail, contact?.DisplayName ?? "Membre du groupe", meeting.Title,
                        meeting.StartAtUtc ?? DateTime.UtcNow, meeting.TimeZoneId, organizer, meeting.Agenda,
                        MemberJoinUrl(meeting, contact?.DisplayName, inv.RecipientEmail),
                        meeting.RecordingEnabled, meeting.AiEnabled, false, null, meeting.AccessCode, ct);
                }

                inv.Status = MeetingInvitationStatus.Sent;
                inv.LastAttemptAtUtc = DateTime.UtcNow;
                inv.AttemptCount++;
            }
            catch (Exception ex)
            {
                inv.LastError = ex.Message;
                inv.AttemptCount++;
                inv.LastAttemptAtUtc = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task MaybeRemind(
        Meeting meeting, MeetingNotificationKind kind, bool enabled, DateTime dueUtc, TimeSpan window, DateTime now, CancellationToken ct)
    {
        if (!enabled) return;
        if (now < dueUtc || now > dueUtc + window) return;
        if (db.MeetingNotifications.Any(n => n.MeetingId == meeting.Id && n.Kind == kind))
            return;
        foreach (var p in db.MeetingParticipants.Where(x => x.MeetingId == meeting.Id && x.UserId != null).ToList())
        {
            var c = await contacts.GetAsync(p.UserId!, ct);
            if (c is null || string.IsNullOrWhiteSpace(c.Value.Email)) continue;
            try
            {
                await email.SendMeetingReminderAsync(
                    c.Value.Email, c.Value.DisplayName, meeting.Title, meeting.StartAtUtc ?? now,
                    MemberJoinUrl(meeting, c.Value.DisplayName, c.Value.Email), meeting.AccessCode, ct);
                db.Add(new MeetingNotification
                {
                    MeetingId = meeting.Id,
                    Kind = kind,
                    RecipientUserId = p.UserId,
                    RecipientEmail = c.Value.Email,
                    SentAtUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                db.Add(new MeetingNotification
                {
                    MeetingId = meeting.Id,
                    Kind = kind,
                    RecipientUserId = p.UserId,
                    RecipientEmail = c.Value.Email,
                    Failed = true,
                    Error = ex.Message
                });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private sealed record Scope(bool Platform, Guid? PrimaryGroupId, HashSet<Guid> GroupIds);

    private Scope ResolveScope(string userId, bool asPlatformAdmin, Guid? actAsGroupId)
    {
        if (asPlatformAdmin)
        {
            var ids = db.ExpertGroups.Where(g => g.IsActive).Select(g => g.Id).ToHashSet();
            return new Scope(true, actAsGroupId, ids);
        }
        var managed = db.ExpertGroupMembers.FirstOrDefault(m =>
            m.UserId == userId && m.Status == ExpertMembershipStatus.Active
            && m.MemberRole == ExpertGroupMemberRole.Manager);
        var groups = db.ExpertGroupMembers
            .Where(m => m.UserId == userId && m.Status == ExpertMembershipStatus.Active)
            .Select(m => m.ExpertGroupId)
            .ToHashSet();
        var primary = actAsGroupId ?? managed?.ExpertGroupId ?? groups.FirstOrDefault();
        return new Scope(false, primary == Guid.Empty ? null : primary, groups);
    }

    private void ValidateVisibility(MeetingVisibility visibility, Scope scope)
    {
        if (visibility == MeetingVisibility.International && !scope.Platform)
            throw new InvalidOperationException("La visibilité internationale est réservée au Super Admin.");
        if (visibility == MeetingVisibility.SelectedGroups && !scope.Platform && scope.GroupIds.Count == 0)
            throw new InvalidOperationException("Aucun groupe accessible.");
    }

    private HashSet<Guid> NormalizeGroups(MeetingVisibility visibility, IReadOnlyList<Guid>? requested, Scope scope)
    {
        var req = (requested ?? []).Where(id => id != Guid.Empty).ToHashSet();
        return visibility switch
        {
            MeetingVisibility.International when scope.Platform => scope.GroupIds,
            MeetingVisibility.SelectedGroups => req.Count == 0
                ? scope.GroupIds
                : req.Where(id => scope.Platform || scope.GroupIds.Contains(id)).ToHashSet(),
            MeetingVisibility.CurrentGroup => scope.PrimaryGroupId is Guid g ? [g] : scope.GroupIds,
            _ => scope.PrimaryGroupId is Guid p ? [p] : scope.GroupIds
        };
    }

    private void EnsureCandidatesInScope(
        IReadOnlyList<string> userIds, HashSet<Guid> groups, Scope scope, MeetingVisibility visibility)
    {
        if (userIds.Count == 0) return;
        var allowedUsers = db.ExpertGroupMembers
            .Where(m => groups.Contains(m.ExpertGroupId) && m.Status == ExpertMembershipStatus.Active)
            .Select(m => m.UserId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in userIds)
        {
            if (!allowedUsers.Contains(id) && visibility != MeetingVisibility.Private)
                throw new InvalidOperationException("Un participant est hors de votre périmètre.");
            if (visibility == MeetingVisibility.Private && !allowedUsers.Contains(id) && !scope.Platform)
                throw new InvalidOperationException("Vous ne pouvez inviter que des membres visibles de votre groupe.");
        }
    }

    private Meeting RequireVisible(string userId, Guid meetingId, bool asPlatformAdmin, Guid? actAsGroupId)
    {
        var meeting = db.Meetings.FirstOrDefault(m => m.Id == meetingId)
            ?? throw new InvalidOperationException("Réunion introuvable.");
        if (asPlatformAdmin) return meeting;
        var scope = ResolveScope(userId, false, actAsGroupId);
        var ok = meeting.OrganizerUserId == userId
            || (meeting.OrganizerGroupId is Guid og && scope.GroupIds.Contains(og))
            || db.MeetingGroups.Any(g => g.MeetingId == meeting.Id && scope.GroupIds.Contains(g.ExpertGroupId))
            || db.MeetingParticipants.Any(p => p.MeetingId == meeting.Id && p.UserId == userId);
        if (!ok) throw new InvalidOperationException("Cette réunion n’est pas dans votre périmètre.");
        return meeting;
    }

    private Meeting RequireModerator(string userId, Guid meetingId)
    {
        var meeting = db.Meetings.FirstOrDefault(m => m.Id == meetingId)
            ?? throw new InvalidOperationException("Réunion introuvable.");
        if (!IsModerator(userId, meeting, false))
            throw new InvalidOperationException("Action réservée à l’organisateur ou au modérateur.");
        return meeting;
    }

    private bool IsModerator(string userId, Meeting meeting, bool asPlatformAdmin)
    {
        if (meeting.OrganizerUserId == userId) return true;
        var p = db.MeetingParticipants.FirstOrDefault(x => x.MeetingId == meeting.Id && x.UserId == userId);
        return p?.Role is MeetingParticipantRole.Organizer or MeetingParticipantRole.CoOrganizer
            || HasPerm(userId, meeting.OrganizerGroupId, asPlatformAdmin, GroupMemberPermissionCatalog.MeetingsModerate);
    }

    private void EnsureCan(string userId, Guid? groupId, bool asPlatformAdmin, string key)
    {
        if (!HasPerm(userId, groupId, asPlatformAdmin, key))
            throw new InvalidOperationException("Permission insuffisante pour cette action.");
    }

    private bool HasPerm(string userId, Guid? groupId, bool asPlatformAdmin, string key)
    {
        var perms = PermissionsFor(userId, groupId, asPlatformAdmin);
        return perms.Contains(key, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> ReadMemberPermissions(ExpertGroupMember member)
    {
        if (!string.IsNullOrWhiteSpace(member.PermissionsJson))
        {
            try
            {
                var stored = JsonSerializer.Deserialize<List<string>>(member.PermissionsJson);
                if (stored is { Count: > 0 })
                    return GroupMemberPermissionCatalog.Sanitize(member.MemberRole, stored);
            }
            catch { /* defaults */ }
        }
        return GroupMemberPermissionCatalog.DefaultsFor(member.MemberRole);
    }

    private async Task<MeetingDetailDto> MapDetailAsync(string userId, Meeting meeting, bool asPlatformAdmin, CancellationToken ct)
    {
        var org = await contacts.GetAsync(meeting.OrganizerUserId, ct);
        var groupIds = db.MeetingGroups.Where(g => g.MeetingId == meeting.Id).Select(g => g.ExpertGroupId).ToList();
        var participants = db.MeetingParticipants.Where(p => p.MeetingId == meeting.Id).ToList();
        var partDtos = new List<MeetingParticipantDto>();
        foreach (var p in participants)
        {
            if (!string.IsNullOrWhiteSpace(p.UserId))
            {
                var c = await contacts.GetAsync(p.UserId, ct);
                var mem = db.ExpertGroupMembers.FirstOrDefault(m => m.UserId == p.UserId && m.Status == ExpertMembershipStatus.Active);
                var grp = mem is null ? null : db.ExpertGroups.FirstOrDefault(g => g.Id == mem.ExpertGroupId);
                partDtos.Add(new MeetingParticipantDto(
                    p.Id, p.UserId, c?.DisplayName ?? p.UserId, c?.Email, RoleFr(mem?.MemberRole ?? ExpertGroupMemberRole.Expert),
                    grp?.Name, grp?.CountryCode, null, p.Role, p.Status, p.DurationSeconds, false));
            }
            else if (p.ExternalGuestId is Guid gid)
            {
                var g = db.MeetingExternalGuests.FirstOrDefault(x => x.Id == gid);
                partDtos.Add(new MeetingParticipantDto(
                    p.Id, null, g?.FullName ?? "Invité", g?.Email, "Invité externe", null, null, null,
                    p.Role, p.Status, p.DurationSeconds, true));
            }
        }
        // Le code en clair reste réservé aux organisateurs et modérateurs : les autres le reçoivent par courriel.
        var canSeeCode = IsModerator(userId, meeting, asPlatformAdmin);
        var guests = db.MeetingExternalGuests.Where(g => g.MeetingId == meeting.Id).ToList()
            .Select(g => new MeetingExternalGuestDto(
                g.Id, g.FullName, g.Email, g.TokenExpiresAtUtc, g.RevokedAtUtc is not null,
                g.VerifiedAtUtc is not null, canSeeCode ? g.AccessCode : null))
            .ToList();
        var rec = db.MeetingRecurrences.Any(r => r.MeetingId == meeting.Id);
        return new MeetingDetailDto(
            meeting.Id, meeting.Title, meeting.Description, meeting.Agenda, meeting.Status, meeting.Visibility,
            meeting.StartAtUtc, meeting.EndAtUtc, meeting.TimeZoneId, meeting.OrganizerUserId, org?.DisplayName ?? "",
            meeting.OrganizerGroupId, meeting.WaitingRoomEnabled, meeting.AllowMic, meeting.AllowCamera, meeting.AllowScreenShare,
            meeting.RecordingEnabled, meeting.TranscriptionEnabled, meeting.AiEnabled, meeting.AiActivatedByOrganizer,
            meeting.Locked, meeting.Language, rec, groupIds, partDtos, guests,
            PermissionsFor(userId, meeting.OrganizerGroupId, asPlatformAdmin),
            meeting.AccessCodeHash is not null,
            canSeeCode ? meeting.AccessCode : null);
    }

    /// <summary>
    /// Lien membre : code et identité portés par l'URL pour que le destinataire n'ait qu'un clic à faire.
    /// </summary>
    private string MemberJoinUrl(Meeting meeting, string? displayName, string? email)
    {
        var url = $"{urls.WebBaseUrl.TrimEnd('/')}/expert/meetings/{meeting.Id}/room";
        return AppendAccess(url, meeting.AccessCode, displayName, email);
    }

    /// <summary>Lien invité : jeton personnel + code personnel, saisie inutile.</summary>
    private string GuestJoinUrl(MeetingExternalGuest guest, string token)
    {
        var url = $"{urls.WebBaseUrl.TrimEnd('/')}/meet/join/{Uri.EscapeDataString(token)}";
        return AppendAccess(url, guest.AccessCode, guest.FullName, guest.Email);
    }

    private static string AppendAccess(string url, string? code, string? name, string? email)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(code)) parts.Add($"code={Uri.EscapeDataString(code)}");
        if (!string.IsNullOrWhiteSpace(name)) parts.Add($"name={Uri.EscapeDataString(name)}");
        if (!string.IsNullOrWhiteSpace(email)) parts.Add($"email={Uri.EscapeDataString(email)}");
        return parts.Count == 0 ? url : $"{url}?{string.Join('&', parts)}";
    }

    private async Task SendInvitesAsync(Meeting meeting, List<(MeetingExternalGuest Guest, string Token)> guests, CancellationToken ct)
    {
        var org = await contacts.GetAsync(meeting.OrganizerUserId, ct);
        var organizer = org?.DisplayName ?? "Organisateur";
        foreach (var p in db.MeetingParticipants.Where(x => x.MeetingId == meeting.Id && x.UserId != null).ToList())
        {
            var c = await contacts.GetAsync(p.UserId!, ct);
            if (c is null || string.IsNullOrWhiteSpace(c.Value.Email)) continue;
            var memberJoin = MemberJoinUrl(meeting, c.Value.DisplayName, c.Value.Email);
            var inv = new MeetingInvitation
            {
                MeetingId = meeting.Id,
                Kind = MeetingInvitationKind.Internal,
                RecipientEmail = c.Value.Email,
                RecipientUserId = p.UserId,
                Status = MeetingInvitationStatus.Pending
            };
            db.Add(inv);
            try
            {
                await email.SendMeetingInvitationAsync(
                    c.Value.Email, c.Value.DisplayName, meeting.Title, meeting.StartAtUtc ?? DateTime.UtcNow,
                    meeting.TimeZoneId, organizer, meeting.Agenda, memberJoin,
                    meeting.RecordingEnabled, meeting.AiEnabled, false, null, meeting.AccessCode, ct);
                inv.Status = MeetingInvitationStatus.Sent;
                inv.LastAttemptAtUtc = DateTime.UtcNow;
                inv.AttemptCount = 1;
                db.Add(new MeetingNotification
                {
                    MeetingId = meeting.Id,
                    Kind = MeetingNotificationKind.Invitation,
                    RecipientUserId = p.UserId,
                    RecipientEmail = c.Value.Email,
                    SentAtUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                inv.Status = MeetingInvitationStatus.Failed;
                inv.LastError = ex.Message;
                inv.AttemptCount = 1;
            }
        }

        foreach (var (guest, token) in guests)
        {
            var join = GuestJoinUrl(guest, token);
            var inv = new MeetingInvitation
            {
                MeetingId = meeting.Id,
                Kind = MeetingInvitationKind.External,
                RecipientEmail = guest.Email,
                ExternalGuestId = guest.Id,
                Status = MeetingInvitationStatus.Pending
            };
            db.Add(inv);
            try
            {
                await email.SendMeetingInvitationAsync(
                    guest.Email, guest.FullName, meeting.Title, meeting.StartAtUtc ?? DateTime.UtcNow,
                    meeting.TimeZoneId, organizer, meeting.Agenda, join,
                    meeting.RecordingEnabled, meeting.AiEnabled, true, guest.TokenExpiresAtUtc, guest.AccessCode, ct);
                inv.Status = MeetingInvitationStatus.Sent;
                inv.AttemptCount = 1;
                inv.LastAttemptAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                inv.Status = MeetingInvitationStatus.Failed;
                inv.LastError = ex.Message;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private void SeedAiDraft(Meeting meeting)
    {
        if (db.MeetingAiSummaries.Any(s => s.MeetingId == meeting.Id && s.IsDraft))
            return;
        var transcript = db.MeetingTranscripts.Where(t => t.MeetingId == meeting.Id).Select(t => t.Content).FirstOrDefault() ?? "";
        var agenda = meeting.Agenda ?? meeting.Description ?? meeting.Title;
        db.Add(new MeetingAISummary
        {
            MeetingId = meeting.Id,
            Overview = string.IsNullOrWhiteSpace(transcript)
                ? $"Compte rendu proposé à partir de l’ordre du jour : {agenda}"
                : Truncate(transcript, 800),
            TopicsJson = JsonSerializer.Serialize(new[] { meeting.Title, "Organisation du groupe", "Suivi pédagogique" }),
            OpenQuestionsJson = JsonSerializer.Serialize(new[] { "Quelles sont les prochaines échéances de validation ?" }),
            RisksJson = JsonSerializer.Serialize(new[] { "Documents enseignants incomplets" }),
            NextSteps = "Valider les actions proposées puis partager le compte rendu.",
            IsDraft = true
        });
        db.Add(new MeetingDecision
        {
            MeetingId = meeting.Id,
            Text = "Poursuivre le suivi des enseignants du groupe selon le calendrier convenu.",
            FromAi = true,
            Accepted = false
        });
        db.Add(new MeetingActionItem
        {
            MeetingId = meeting.Id,
            Title = "Vérifier les documents de trois nouveaux enseignants",
            AssigneeName = "Administrateur du groupe",
            DueAtUtc = DateTime.UtcNow.Date.AddDays(10),
            Status = MeetingActionItemStatus.Proposed,
            FromAi = true
        });
    }

    private MeetingExternalGuest FindGuestByToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Lien d’invitation manquant.");
        var hash = Hash(token.Trim());
        return db.MeetingExternalGuests.FirstOrDefault(g => g.TokenHash == hash)
            ?? throw new InvalidOperationException("Invitation introuvable ou expirée.");
    }

    private void Audit(Guid meetingId, string userId, string action, string? detail)
    {
        db.Add(new MeetingAuditLog
        {
            MeetingId = meetingId,
            ActorUserId = userId,
            Action = action,
            Detail = detail
        });
    }

    private static string RoleFr(ExpertGroupMemberRole role) => role switch
    {
        ExpertGroupMemberRole.Manager => "Responsable",
        ExpertGroupMemberRole.Senior => "Expert senior",
        ExpertGroupMemberRole.Observer => "Observateur",
        ExpertGroupMemberRole.DisciplineLead => "Chef discipline",
        ExpertGroupMemberRole.CommitteeLead => "Chef comité",
        _ => "Expert"
    };

    /// <summary>Applique le code fourni par l'organisateur, ou en génère un si la saisie est vide.</summary>
    private static string ApplyAccessCode(Meeting meeting, string? requested)
    {
        var code = NormalizeAccessCode(requested) ?? GenerateAccessCode();
        meeting.AccessCode = code;
        meeting.AccessCodeHash = Hash(code);
        return code;
    }

    private static void EnsureAccessCodeMatches(Meeting meeting, string? provided)
    {
        if (meeting.AccessCodeHash is null) return;
        var code = NormalizeAccessCode(provided);
        if (code is null || !SlowEquals(meeting.AccessCodeHash, Hash(code)))
            throw new InvalidOperationException("Code de réunion incorrect.");
    }

    private static string? NormalizeAccessCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var cleaned = new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (cleaned.Length is < 4 or > 16)
            throw new InvalidOperationException("Le code doit contenir de 4 à 16 lettres ou chiffres.");
        return cleaned;
    }

    /// <summary>Alphabet sans caractères ambigus (0/O, 1/I) pour un code dicté au téléphone.</summary>
    private static string GenerateAccessCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[6];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool SlowEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    private static string? TrimOrNull(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length <= max ? t : t[..max];
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
    private static IReadOnlyList<string> ReadJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
