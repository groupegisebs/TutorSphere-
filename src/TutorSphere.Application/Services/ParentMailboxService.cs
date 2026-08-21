using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Application.DTOs.Parents;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IParentMailboxService
{
    Task<ParentMailboxDto> GetMailboxAsync(string parentUserId, CancellationToken ct = default);
    Task<ParentMailboxDirectoryDto?> GetDirectoryAsync(string parentUserId, Guid childId, CancellationToken ct = default);
    Task<ParentMailboxThreadDetailDto?> GetThreadAsync(string parentUserId, string threadId, CancellationToken ct = default);
    Task<ParentMailboxThreadDetailDto> ComposeAsync(
        string parentUserId,
        ParentMailboxComposeRequest request,
        CancellationToken ct = default);
    Task<ParentMailboxMessageDto> ReplyAsync(
        string parentUserId,
        string threadId,
        ParentMailboxReplyRequest request,
        CancellationToken ct = default);
    Task MarkReadAsync(string parentUserId, string threadId, CancellationToken ct = default);
}

public sealed class ParentMailboxService(
    IApplicationDbContext db,
    ISupportInboxResolver inbox,
    IRealTimeMessaging realtime,
    IExpertGroupService groups) : IParentMailboxService
{
    private static readonly HashSet<string> TeacherReasons = ["lessons", "homework", "results", "progress"];
    private static readonly HashSet<string> GroupReasons = ["schedule", "replacement", "quality", "organization", "complaint"];
    private static readonly HashSet<string> AdminReasons = ["account", "payment", "security", "technical"];

    public Task<ParentMailboxDto> GetMailboxAsync(string parentUserId, CancellationToken ct = default)
    {
        var parent = RequireParent(parentUserId);
        var children = LoadChildren(parent.Id);
        var childMap = children.ToDictionary(c => c.Id);
        var adminId = inbox.ResolveUserIdAsync(ct).GetAwaiter().GetResult();

        var messages = db.Messages
            .Where(m => m.SenderUserId == parentUserId || m.RecipientUserId == parentUserId)
            .OrderByDescending(m => m.CreatedAt)
            .ToList();

        var threads = messages
            .GroupBy(m => ThreadKey(m, parentUserId))
            .Select(g =>
            {
                var last = g.First();
                var channel = NormalizeChannel(last.ParentChannel);
                var student = last.StudentId is Guid sid && childMap.TryGetValue(sid, out var c) ? c : null;
                var peerId = PeerOf(last, parentUserId);
                var (name, role, group, verified, slug) = ResolvePeerCard(channel, peerId, student, last);
                return new ParentMailboxThreadDto(
                    MakeThreadId(channel, last.StudentId, peerId),
                    channel,
                    last.StudentId,
                    student?.FirstName ?? "",
                    peerId,
                    name,
                    role,
                    SubjectOf(channel, last.ParentReason, last.CaseNumber),
                    group,
                    verified,
                    slug,
                    last.CaseNumber,
                    last.ParentReason,
                    Preview(last.Body),
                    last.CreatedAt,
                    g.Count(m => m.RecipientUserId == parentUserId && !m.IsRead));
            })
            .OrderByDescending(t => t.LastAt ?? DateTime.MinValue)
            .ToList();

        var childDtos = children
            .Select(s => new ParentMailboxChildDto(s.Id, s.FirstName, s.LastName, s.SchoolLevel, s.PhotoUrl))
            .ToList();

        return Task.FromResult(new ParentMailboxDto(childDtos, threads, adminId is not null));
    }

    public Task<ParentMailboxDirectoryDto?> GetDirectoryAsync(
        string parentUserId,
        Guid childId,
        CancellationToken ct = default)
    {
        var parent = RequireParent(parentUserId);
        var child = db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == childId && s.ParentProfileId == parent.Id);
        if (child is null)
            return Task.FromResult<ParentMailboxDirectoryDto?>(null);

        var teachers = TeachersForChild(child);
        ParentMailboxGroupDto? group = null;
        var firstTeacher = teachers.FirstOrDefault();
        if (firstTeacher is not null)
            group = ResolveGroup(firstTeacher.UserId);

        var now = DateTime.UtcNow;
        var context = new List<ParentMailboxContextItemDto>();
        var nextHw = db.HomeworksForAnyTenant
            .Where(h => h.StudentId == child.Id && !h.IsDraft && h.DueDate != null && h.DueDate > now && !h.IsGraded)
            .OrderBy(h => h.DueDate)
            .FirstOrDefault();
        if (nextHw is not null)
            context.Add(new ParentMailboxContextItemDto("homework", $"Devoir : {nextHw.Title}"));

        var nextLesson = NextLesson(child);
        if (nextLesson is not null)
            context.Add(new ParentMailboxContextItemDto("lesson", $"Prochain cours : {nextLesson.Value.label}"));

        var homework = db.HomeworksForAnyTenant
            .Where(h => h.StudentId == child.Id && !h.IsDraft)
            .OrderByDescending(h => h.CreatedAt)
            .Take(12)
            .Select(h => new ParentMailboxPickDto(h.Id, string.IsNullOrWhiteSpace(h.Subject) ? h.Title : $"{h.Subject} — {h.Title}"))
            .ToList();
        var documents = db.DocumentsForAnyTenant
            .Where(d => d.StudentId == child.Id)
            .OrderByDescending(d => d.CreatedAt)
            .Take(12)
            .Select(d => new ParentMailboxPickDto(d.Id, d.Name))
            .ToList();

        var adminId = inbox.ResolveUserIdAsync(ct).GetAwaiter().GetResult();
        return Task.FromResult<ParentMailboxDirectoryDto?>(new ParentMailboxDirectoryDto(
            child.Id,
            child.FirstName,
            teachers,
            group,
            adminId is not null,
            context,
            homework,
            documents));
    }

    public Task<ParentMailboxThreadDetailDto?> GetThreadAsync(
        string parentUserId,
        string threadId,
        CancellationToken ct = default)
    {
        if (!TryParseThreadId(threadId, out var channel, out var studentId, out var peerId))
            return Task.FromResult<ParentMailboxThreadDetailDto?>(null);

        RequireParent(parentUserId);
        var mailbox = GetMailboxAsync(parentUserId, ct).GetAwaiter().GetResult();
        var thread = mailbox.Threads.FirstOrDefault(t => t.ThreadId == MakeThreadId(channel, studentId, peerId));
        if (thread is null)
            return Task.FromResult<ParentMailboxThreadDetailDto?>(null);

        var messages = LoadThreadMessages(parentUserId, channel, studentId, peerId)
            .Select(m => MapMessage(m, parentUserId))
            .ToList();
        return Task.FromResult<ParentMailboxThreadDetailDto?>(new ParentMailboxThreadDetailDto(thread, messages));
    }

    public async Task<ParentMailboxThreadDetailDto> ComposeAsync(
        string parentUserId,
        ParentMailboxComposeRequest request,
        CancellationToken ct = default)
    {
        var parent = RequireParent(parentUserId);
        var child = db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == request.ChildId && s.ParentProfileId == parent.Id)
            ?? throw new InvalidOperationException("Enfant introuvable.");

        var channel = NormalizeChannel(request.Channel);
        ValidateReason(channel, request.Reason);
        var body = RequireBody(request.Body, request.AppointmentAt);

        var (peerId, tenantId, _) = await ResolveRecipientAsync(channel, child, request.TeacherUserId, ct);
        var (attachType, attachId, attachLabel) = ResolveAttachment(child.Id, request);

        string? caseNumber = null;
        if (channel == "admin")
            caseNumber = AllocateCaseNumber();

        var subject = BuildSubject(child.FirstName, channel, request.Reason, caseNumber);
        var storedBody = PrefixBody(child.FirstName, channel, request.Reason, caseNumber, body, attachLabel);

        var message = new Message
        {
            TenantId = tenantId,
            SenderUserId = parentUserId,
            RecipientUserId = peerId,
            Subject = subject,
            Body = storedBody,
            ParentChannel = channel,
            StudentId = child.Id,
            ParentReason = request.Reason,
            CaseNumber = caseNumber,
            AttachmentType = attachType,
            AttachmentId = attachId,
            AttachmentLabel = attachLabel
        };
        db.Add(message);

        if (channel == "admin")
        {
            db.Add(new ParentSupportRequest
            {
                ParentProfileId = parent.Id,
                UserId = parentUserId,
                Subject = subject,
                Message = body,
                Status = ParentSupportRequestStatus.Open,
                CaseNumber = caseNumber,
                StudentId = child.Id,
                Reason = request.Reason
            });
        }

        await db.SaveChangesAsync(ct);
        await NotifyAsync(peerId, message, ct);

        return await GetThreadAsync(parentUserId, MakeThreadId(channel, child.Id, peerId), ct)
            ?? throw new InvalidOperationException("Conversation introuvable après envoi.");
    }

    public async Task<ParentMailboxMessageDto> ReplyAsync(
        string parentUserId,
        string threadId,
        ParentMailboxReplyRequest request,
        CancellationToken ct = default)
    {
        if (!TryParseThreadId(threadId, out var channel, out var studentId, out var peerId))
            throw new InvalidOperationException("Conversation introuvable.");

        var parent = RequireParent(parentUserId);
        var last = LoadThreadMessages(parentUserId, channel, studentId, peerId).LastOrDefault()
            ?? throw new InvalidOperationException("Ouvrez d'abord la conversation.");

        Student? child = studentId is Guid sid
            ? db.StudentsForAnyTenant.FirstOrDefault(s => s.Id == sid && s.ParentProfileId == parent.Id)
            : null;
        if (studentId is not null && child is null)
            throw new InvalidOperationException("Enfant introuvable.");

        var body = RequireBody(request.Body, request.AppointmentAt);
        var attach = child is null
            ? (null, null, (string?)null)
            : ResolveAttachment(child.Id, new ParentMailboxComposeRequest(
                child.Id, channel, last.ParentReason ?? "lessons", body,
                last.SenderUserId == parentUserId ? last.RecipientUserId : last.SenderUserId,
                request.AttachmentType, request.AttachmentId, request.AppointmentAt));

        var storedBody = PrefixBody(
            child?.FirstName ?? "",
            channel,
            last.ParentReason,
            last.CaseNumber,
            body,
            attach.Item3);

        var message = new Message
        {
            TenantId = last.TenantId,
            SenderUserId = parentUserId,
            RecipientUserId = peerId,
            Subject = last.Subject,
            Body = storedBody,
            ParentChannel = channel,
            StudentId = studentId,
            ParentReason = last.ParentReason,
            CaseNumber = last.CaseNumber,
            AttachmentType = attach.Item1,
            AttachmentId = attach.Item2,
            AttachmentLabel = attach.Item3
        };
        db.Add(message);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(peerId, message, ct);
        return MapMessage(message, parentUserId);
    }

    public Task MarkReadAsync(string parentUserId, string threadId, CancellationToken ct = default)
    {
        if (!TryParseThreadId(threadId, out var channel, out var studentId, out var peerId))
            return Task.CompletedTask;

        RequireParent(parentUserId);
        var unread = LoadThreadMessages(parentUserId, channel, studentId, peerId)
            .Where(m => m.RecipientUserId == parentUserId && !m.IsRead)
            .ToList();
        foreach (var m in unread)
        {
            m.IsRead = true;
            m.ReadAt = DateTime.UtcNow;
        }

        return unread.Count == 0 ? Task.CompletedTask : db.SaveChangesAsync(ct);
    }

    private async Task<(string PeerId, Guid TenantId, ParentMailboxGroupDto? Group)> ResolveRecipientAsync(
        string channel,
        Student child,
        string? teacherUserId,
        CancellationToken ct)
    {
        if (channel == "admin")
        {
            var adminId = await inbox.ResolveUserIdAsync(ct)
                ?? throw new InvalidOperationException("L'assistance TutorSphere n'est pas disponible pour le moment.");
            return (adminId, child.TenantId, null);
        }

        var teachers = TeachersForChild(child);
        if (teachers.Count == 0)
            throw new InvalidOperationException("Aucun enseignant assigné à cet enfant.");

        var teacher = string.IsNullOrWhiteSpace(teacherUserId)
            ? teachers[0]
            : teachers.FirstOrDefault(t => t.UserId == teacherUserId)
              ?? throw new InvalidOperationException("Cet enseignant n'est pas assigné à l'enfant.");

        var tenant = db.Tenants.FirstOrDefault(t => t.OwnerUserId == teacher.UserId && t.Slug != "platform-parents")
            ?? db.Tenants.FirstOrDefault(t => t.Id == child.TenantId)
            ?? throw new InvalidOperationException("Espace enseignant introuvable.");

        if (channel == "teacher")
            return (teacher.UserId, tenant.Id, null);

        var group = ResolveGroup(teacher.UserId)
            ?? throw new InvalidOperationException("Aucun groupe responsable n'est lié à cet enseignant.");
        if (string.IsNullOrWhiteSpace(group.ManagerUserId))
            throw new InvalidOperationException("Le groupe responsable n'est pas encore joignable.");

        return (group.ManagerUserId, tenant.Id, group);
    }

    private List<ParentMailboxTeacherDto> TeachersForChild(Student child)
    {
        var tenantIds = new HashSet<Guid> { child.TenantId };
        foreach (var tid in db.StudentSubscriptionsForAnyTenant
                     .Where(s => s.StudentId == child.Id
                                 && s.Status != SubscriptionStatus.Cancelled
                                 && s.Status != SubscriptionStatus.Rejected
                                 && s.Status != SubscriptionStatus.Expired)
                     .Select(s => s.TenantId))
            tenantIds.Add(tid);

        var lessonTenants = db.LessonAttendancesForAnyTenant
            .Where(a => a.StudentId == child.Id)
            .Join(db.LessonsForAnyTenant, a => a.LessonId, l => l.Id, (_, l) => l)
            .Select(l => new { l.TenantId, l.Subject })
            .ToList();
        foreach (var row in lessonTenants)
            tenantIds.Add(row.TenantId);

        var tenants = db.Tenants
            .Where(t => tenantIds.Contains(t.Id)
                        && t.Slug != "platform-parents"
                        && t.OwnerUserId != null
                        && t.OwnerUserId != "")
            .ToList();

        var result = new List<ParentMailboxTeacherDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tenant in tenants)
        {
            if (!seen.Add(tenant.OwnerUserId))
                continue;
            var subject = lessonTenants.FirstOrDefault(l => l.TenantId == tenant.Id)?.Subject;
            var group = ResolveGroupFromTenant(tenant);
            result.Add(new ParentMailboxTeacherDto(
                tenant.OwnerUserId,
                DisplayTeacherName(tenant),
                subject,
                tenant.IsPublicProfile ? tenant.Slug : null,
                group?.Name,
                group?.Verified ?? false));
        }

        return result;
    }

    private ParentMailboxGroupDto? ResolveGroup(string teacherUserId)
    {
        var tenant = db.Tenants.FirstOrDefault(t => t.OwnerUserId == teacherUserId && t.Slug != "platform-parents");
        return tenant is null ? null : ResolveGroupFromTenant(tenant);
    }

    private ParentMailboxGroupDto? ResolveGroupFromTenant(Tenant tenant)
    {
        ExpertGroup? group = null;
        if (tenant.ApprovedByExpertGroupId is Guid gid)
            group = db.ExpertGroups.FirstOrDefault(g => g.Id == gid && g.IsActive);

        group ??= groups.ResolveReviewerGroup(tenant.Country);
        if (group is null)
            return null;

        var manager = db.ExpertGroupManagerMandates
            .Where(m => m.ExpertGroupId == group.Id && m.Status == ExpertGroupManagerMandateStatus.Active)
            .Select(m => m.UserId)
            .FirstOrDefault();

        return new ParentMailboxGroupDto(
            string.IsNullOrWhiteSpace(manager) ? null : manager,
            group.Id,
            group.Name,
            group.LogoUrl,
            tenant.ExpertApprovalStatus == ExpertApprovalStatus.Approved && tenant.ApprovedByExpertGroupId == group.Id,
            tenant.OwnerUserId);
    }

    private ParentMailboxGroupDto? ResolveGroupFromPeer(string peerId)
    {
        var mandate = db.ExpertGroupManagerMandates
            .FirstOrDefault(m => m.UserId == peerId && m.Status == ExpertGroupManagerMandateStatus.Active);
        if (mandate is null)
            return null;
        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == mandate.ExpertGroupId);
        if (group is null)
            return null;
        return new ParentMailboxGroupDto(peerId, group.Id, group.Name, group.LogoUrl, true, null);
    }

    private (string Name, string Role, string? Group, bool Verified, string? Slug) ResolvePeerCard(
        string channel,
        string peerId,
        Student? student,
        Message last)
    {
        if (channel == "admin")
            return ("Administration TutorSphere", "Assistance", null, true, null);

        if (channel == "group")
        {
            ParentMailboxGroupDto? group = null;
            if (student is not null)
            {
                var teacherId = TeachersForChild(student).FirstOrDefault()?.UserId;
                if (!string.IsNullOrWhiteSpace(teacherId))
                    group = ResolveGroup(teacherId);
            }
            group ??= ResolveGroupFromPeer(peerId);
            return (group?.Name ?? "Groupe responsable", "Groupe responsable", group?.Name, group?.Verified ?? false, null);
        }

        var teacherTenant = db.Tenants.FirstOrDefault(t => t.OwnerUserId == peerId && t.Slug != "platform-parents");
        var g = teacherTenant is null ? null : ResolveGroupFromTenant(teacherTenant);
        return (
            teacherTenant is null ? "Enseignant" : DisplayTeacherName(teacherTenant),
            "Enseignant",
            g?.Name,
            g?.Verified ?? false,
            teacherTenant is { IsPublicProfile: true } ? teacherTenant.Slug : null);
    }

    private (DateTime start, string label)? NextLesson(Student child)
    {
        var now = DateTime.UtcNow;
        var attendanceLessonIds = db.LessonAttendancesForAnyTenant
            .Where(a => a.StudentId == child.Id)
            .Select(a => a.LessonId);
        var lesson = db.LessonsForAnyTenant
            .Where(l => attendanceLessonIds.Contains(l.Id)
                        && l.StartTime >= now
                        && l.SettlementStatus != LessonSettlementStatus.CancelledFree)
            .OrderBy(l => l.StartTime)
            .FirstOrDefault();
        if (lesson is null)
            return null;
        var local = lesson.StartTime.Kind == DateTimeKind.Utc ? lesson.StartTime.ToLocalTime() : lesson.StartTime;
        var when = local.Date == DateTime.Today
            ? $"Aujourd'hui {local:HH:mm}"
            : local.ToString("g");
        return (lesson.StartTime, when);
    }

    private List<Message> LoadThreadMessages(string parentUserId, string channel, Guid? studentId, string peerId) =>
        db.Messages
            .Where(m => m.SenderUserId == parentUserId || m.RecipientUserId == parentUserId)
            .ToList()
            .Where(m => NormalizeChannel(m.ParentChannel) == channel
                        && m.StudentId == studentId
                        && PeerOf(m, parentUserId) == peerId)
            .OrderBy(m => m.CreatedAt)
            .ToList();

    private (string? Type, Guid? Id, string? Label) ResolveAttachment(Guid childId, ParentMailboxComposeRequest request)
    {
        var type = string.IsNullOrWhiteSpace(request.AttachmentType) ? null : request.AttachmentType.Trim().ToLowerInvariant();
        if (request.AppointmentAt is DateTime when)
        {
            var local = when.Kind == DateTimeKind.Utc ? when.ToLocalTime() : when;
            return ("appointment", null, $"Rendez-vous demandé le {local:g}");
        }

        if (type is null)
            return (null, null, null);

        if (type == "homework")
        {
            var hw = db.HomeworksForAnyTenant.FirstOrDefault(h => h.Id == request.AttachmentId && h.StudentId == childId && !h.IsDraft)
                ?? throw new InvalidOperationException("Devoir introuvable pour cet enfant.");
            return ("homework", hw.Id, hw.Title);
        }

        if (type == "document")
        {
            var doc = db.DocumentsForAnyTenant.FirstOrDefault(d => d.Id == request.AttachmentId && d.StudentId == childId)
                ?? throw new InvalidOperationException("Document introuvable pour cet enfant.");
            return ("document", doc.Id, doc.Name);
        }

        if (type == "appointment")
            return ("appointment", null, "Demande de rendez-vous");

        throw new InvalidOperationException("Pièce jointe non autorisée.");
    }

    private string AllocateCaseNumber()
    {
        var seq = 2048 + db.ParentSupportRequests.Count();
        string code;
        do
        {
            code = $"TS-{seq}";
            seq++;
        } while (db.ParentSupportRequests.Any(r => r.CaseNumber == code));
        return code;
    }

    private ParentProfile RequireParent(string userId) =>
        db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == userId)
        ?? throw new InvalidOperationException("Profil parent introuvable.");

    private List<Student> LoadChildren(Guid parentId) =>
        db.StudentsForAnyTenant
            .Where(s => s.ParentProfileId == parentId)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToList();

    private async Task NotifyAsync(string peerId, Message message, CancellationToken ct)
    {
        var dto = new MessageDto(
            message.Id,
            message.SenderUserId,
            message.RecipientUserId,
            message.Subject,
            TeacherContactPrivacy.RedactFromPublicText(message.Body) ?? message.Body,
            message.IsRead,
            message.ReadAt,
            message.CreatedAt);
        await realtime.NotifyMessageReceivedAsync(peerId, dto, ct);
    }

    private static ParentMailboxMessageDto MapMessage(Message m, string parentUserId) =>
        new(
            m.Id,
            m.SenderUserId == parentUserId,
            TeacherContactPrivacy.RedactFromPublicText(StripPrefix(m.Body)) ?? "",
            m.AttachmentType,
            m.AttachmentLabel,
            m.AttachmentId,
            m.CreatedAt,
            m.IsRead);

    private static string DisplayTeacherName(Tenant tenant)
    {
        var name = string.IsNullOrWhiteSpace(tenant.Name) ? "Enseignant" : tenant.Name.Trim();
        return TeacherContactPrivacy.RedactFromPublicText(name) ?? "Enseignant";
    }

    private static string RequireBody(string? body, DateTime? appointmentAt)
    {
        var text = (body ?? "").Trim();
        if (appointmentAt.HasValue && text.Length == 0)
            return "Demande de rendez-vous.";
        if (text.Length < 2)
            throw new InvalidOperationException("Le message est trop court.");
        return text[..Math.Min(text.Length, 4000)];
    }

    private static void ValidateReason(string channel, string? reason)
    {
        var key = (reason ?? "").Trim().ToLowerInvariant();
        var ok = channel switch
        {
            "teacher" => TeacherReasons.Contains(key),
            "group" => GroupReasons.Contains(key),
            "admin" => AdminReasons.Contains(key),
            _ => false
        };
        if (!ok)
            throw new InvalidOperationException("Choisissez un motif adapté au destinataire.");
    }

    private static string NormalizeChannel(string? channel) =>
        (channel ?? "teacher").Trim().ToLowerInvariant() switch
        {
            "group" => "group",
            "admin" => "admin",
            "teacher" => "teacher",
            _ => "teacher"
        };

    private static string MakeThreadId(string channel, Guid? studentId, string peerId) =>
        $"{channel}:{(studentId is Guid id ? id.ToString("N") : "none")}:{peerId}";

    internal static bool TryParseThreadId(string? value, out string channel, out Guid? studentId, out string peerId)
    {
        channel = "teacher";
        studentId = null;
        peerId = "";
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var parts = value.Split(':', 3);
        if (parts.Length < 3)
            return false;
        channel = NormalizeChannel(parts[0]);
        studentId = parts[1] != "none" && Guid.TryParse(parts[1], out var sid) ? sid : null;
        peerId = parts[2];
        return !string.IsNullOrWhiteSpace(peerId);
    }

    private static (string Channel, Guid? StudentId, string Peer) ThreadKey(Message m, string parentUserId) =>
        (NormalizeChannel(m.ParentChannel), m.StudentId, PeerOf(m, parentUserId));

    private static string PeerOf(Message m, string parentUserId) =>
        m.SenderUserId == parentUserId ? m.RecipientUserId : m.SenderUserId;

    private static string SubjectOf(string channel, string? reason, string? caseNumber) =>
        channel == "admin" && !string.IsNullOrWhiteSpace(caseNumber)
            ? $"Dossier {caseNumber}"
            : reason ?? "";

    private static string BuildSubject(string childFirstName, string channel, string reason, string? caseNumber)
    {
        var who = string.IsNullOrWhiteSpace(childFirstName) ? "Enfant" : childFirstName.Trim();
        return channel == "admin"
            ? $"[{who}] {caseNumber} {reason}"
            : $"[{who}] {reason}";
    }

    private static string PrefixBody(
        string childFirstName,
        string channel,
        string? reason,
        string? caseNumber,
        string body,
        string? attachment)
    {
        var header = $"Concernant {childFirstName}".Trim();
        if (!string.IsNullOrWhiteSpace(reason))
            header += $" — {reason}";
        if (!string.IsNullOrWhiteSpace(caseNumber))
            header += $" ({caseNumber})";
        var extra = string.IsNullOrWhiteSpace(attachment) ? "" : $"\n[{attachment}]";
        return $"{header}{extra}\n\n{body}";
    }

    private static string StripPrefix(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "";
        var idx = body.IndexOf("\n\n", StringComparison.Ordinal);
        return idx >= 0 && body.StartsWith("Concernant ", StringComparison.Ordinal) ? body[(idx + 2)..] : body;
    }

    private static string Preview(string body)
    {
        var text = StripPrefix(body).Replace('\n', ' ').Trim();
        return text.Length <= 90 ? text : $"{text[..87]}…";
    }
}
