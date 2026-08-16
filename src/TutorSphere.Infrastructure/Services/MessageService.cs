using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Infrastructure.Services;

public class MessageService : IMessageService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRealTimeMessaging _realTimeMessaging;

    public MessageService(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        UserManager<ApplicationUser> userManager,
        IRealTimeMessaging realTimeMessaging)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userManager = userManager;
        _realTimeMessaging = realTimeMessaging;
    }

    public async Task<MessageDto> SendAsync(string senderUserId, SendMessageRequest request, CancellationToken ct = default)
    {
        if (senderUserId == request.RecipientUserId)
            throw new InvalidOperationException("Impossible d'envoyer un message à vous-même.");

        var senderRoles = await GetRolesAsync(senderUserId);
        Guid tenantId;

        if (IsActiveGroupManager(senderUserId) || IsActiveGroupMember(senderUserId))
        {
            tenantId = await ResolveTenantForPartyAsync(senderUserId, request.RecipientUserId, ct);
            await EnsureGroupMessagingAllowedAsync(senderUserId, request.RecipientUserId, ct);
        }
        else
        {
            tenantId = RequireTenant();
            await EnsureUserInTenantAsync(senderUserId, tenantId, ct);
            await EnsureRecipientReachableAsync(senderUserId, request.RecipientUserId, tenantId, ct);
        }

        var message = new Message
        {
            TenantId = tenantId,
            SenderUserId = senderUserId,
            RecipientUserId = request.RecipientUserId,
            Subject = request.Subject.Trim(),
            Body = request.Body.Trim()
        };

        _db.Add(message);
        await _db.SaveChangesAsync(ct);

        var dto = MapToDto(message);
        await _realTimeMessaging.NotifyMessageReceivedAsync(request.RecipientUserId, dto, ct);
        return dto;
    }

    public async Task<IReadOnlyList<MessageRecipientDto>> SearchRecipientsAsync(
        string userId, string? query, CancellationToken ct = default)
    {
        var roles = await GetRolesAsync(userId);
        var q = (query ?? "").Trim();

        if (IsActiveGroupManager(userId))
            return await SearchForGroupManagerAsync(userId, q, ct);

        if (IsActiveGroupMember(userId))
            return await SearchForGroupMemberAsync(userId, q, ct);

        RequireTenant();

        if (IsStudent(roles))
            return await SearchTeachersForLearnerAsync(userId, q, ct);
        if (IsTeacher(roles))
            return await SearchContactsForTeacherAsync(q, ct);
        if (IsParent(roles))
            return await SearchTeachersForLearnerAsync(userId, q, ct);

        throw new InvalidOperationException(
            "La recherche de destinataires n'est pas disponible pour ce rôle.");
    }

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(string userId, CancellationToken ct = default)
    {
        EnsureCanAccessMailbox(userId);

        var messages = await _db.Messages
            .Where(m => m.SenderUserId == userId || m.RecipientUserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        var conversations = new List<ConversationDto>();
        foreach (var group in messages.GroupBy(m => m.SenderUserId == userId ? m.RecipientUserId : m.SenderUserId))
        {
            var last = group.First();
            var user = await _userManager.FindByIdAsync(group.Key);
            conversations.Add(new ConversationDto(
                group.Key,
                user?.FullName ?? user?.Email ?? group.Key,
                MapToDto(last),
                group.Count(m => m.RecipientUserId == userId && !m.IsRead)));
        }

        return conversations;
    }

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(
        string userId,
        string otherUserId,
        CancellationToken ct = default)
    {
        EnsureCanAccessMailbox(userId);

        var messages = await _db.Messages
            .Where(m =>
                (m.SenderUserId == userId && m.RecipientUserId == otherUserId) ||
                (m.SenderUserId == otherUserId && m.RecipientUserId == userId))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        return messages.Select(MapToDto).ToList();
    }

    public async Task<MessageDto?> MarkAsReadAsync(string userId, Guid messageId, CancellationToken ct = default)
    {
        EnsureCanAccessMailbox(userId);

        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message is null || message.RecipientUserId != userId)
            return null;

        if (!message.IsRead)
        {
            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
            message.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return MapToDto(message);
    }

    public async Task MarkConversationAsReadAsync(string userId, string otherUserId, CancellationToken ct = default)
    {
        EnsureCanAccessMailbox(userId);

        var unread = await _db.Messages
            .Where(m => m.SenderUserId == otherUserId && m.RecipientUserId == userId && !m.IsRead)
            .ToListAsync(ct);

        if (unread.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var message in unread)
        {
            message.IsRead = true;
            message.ReadAt = now;
            message.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<MessageDto> SendAsPlatformAdminAsync(
        string adminUserId, SendMessageRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientUserId))
            throw new InvalidOperationException("Destinataire requis.");
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            throw new InvalidOperationException("Objet et message sont obligatoires.");
        if (adminUserId == request.RecipientUserId)
            throw new InvalidOperationException("Impossible d'envoyer un message à vous-même.");

        var recipient = await _userManager.FindByIdAsync(request.RecipientUserId)
            ?? throw new InvalidOperationException("Destinataire introuvable.");

        var tenantId = await ResolveTenantForAdminMessageAsync(recipient, ct);

        var message = new Message
        {
            TenantId = tenantId,
            SenderUserId = adminUserId,
            RecipientUserId = recipient.Id,
            Subject = request.Subject.Trim(),
            Body = request.Body.Trim()
        };

        _db.Add(message);
        await _db.SaveChangesAsync(ct);

        var dto = MapToDto(message);
        await _realTimeMessaging.NotifyMessageReceivedAsync(recipient.Id, dto, ct);
        return dto;
    }

    public async Task<IReadOnlyList<ConversationDto>> GetAdminConversationsAsync(
        string adminUserId, CancellationToken ct = default)
    {
        var messages = await _db.Messages
            .Where(m => m.SenderUserId == adminUserId || m.RecipientUserId == adminUserId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        var conversations = new List<ConversationDto>();
        foreach (var group in messages.GroupBy(m => m.SenderUserId == adminUserId ? m.RecipientUserId : m.SenderUserId))
        {
            var last = group.First();
            var user = await _userManager.FindByIdAsync(group.Key);
            conversations.Add(new ConversationDto(
                group.Key,
                user?.FullName ?? user?.Email ?? group.Key,
                MapToDto(last),
                group.Count(m => m.RecipientUserId == adminUserId && !m.IsRead)));
        }

        return conversations;
    }

    public async Task<IReadOnlyList<MessageDto>> GetAdminMessagesAsync(
        string adminUserId, string otherUserId, CancellationToken ct = default)
    {
        var messages = await _db.Messages
            .Where(m =>
                (m.SenderUserId == adminUserId && m.RecipientUserId == otherUserId) ||
                (m.SenderUserId == otherUserId && m.RecipientUserId == adminUserId))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var unread = messages.Where(m => m.RecipientUserId == adminUserId && !m.IsRead).ToList();
        if (unread.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var m in unread)
            {
                m.IsRead = true;
                m.ReadAt = now;
                m.UpdatedAt = now;
            }
            await _db.SaveChangesAsync(ct);
        }

        return messages.Select(MapToDto).ToList();
    }

    private async Task EnsureRecipientReachableAsync(
        string senderId, string recipientId, Guid tenantId, CancellationToken ct)
    {
        _ = await _userManager.FindByIdAsync(recipientId)
            ?? throw new InvalidOperationException("Destinataire introuvable.");

        var senderRoles = await GetRolesAsync(senderId);
        var recipientRoles = await GetRolesAsync(recipientId);

        var roleOk =
            (IsStudent(senderRoles) && IsTeacher(recipientRoles)) ||
            (IsTeacher(senderRoles) && IsStudent(recipientRoles)) ||
            (IsParent(senderRoles) && IsTeacher(recipientRoles)) ||
            (IsTeacher(senderRoles) && IsParent(recipientRoles));

        if (!roleOk)
        {
            throw new InvalidOperationException(
                "Messagerie limitée : les élèves discutent uniquement avec les enseignants, et inversement.");
        }

        if (IsTeacher(senderRoles) && IsStudent(recipientRoles))
        {
            var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.UserId == recipientId);
            if (student is null)
                throw new InvalidOperationException("Élève introuvable.");
            if (student.TenantId == tenantId)
                return;
            if (_db.StudentSubscriptions.Any(s => s.StudentId == student.Id && s.TenantId == tenantId))
                return;
            throw new InvalidOperationException("Cet élève n'est pas lié à votre école.");
        }

        if ((IsStudent(senderRoles) || IsParent(senderRoles)) && IsTeacher(recipientRoles))
        {
            var teachers = await ResolveLinkedTeacherUserIdsAsync(senderId, ct);
            if (!teachers.Contains(recipientId))
                throw new InvalidOperationException("Vous ne pouvez écrire qu'à vos enseignants.");
            return;
        }

        if (IsTeacher(senderRoles) && IsParent(recipientRoles))
        {
            var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == recipientId);
            if (parent is null)
                throw new InvalidOperationException("Parent introuvable.");
            var hasChildHere = _db.StudentsForAnyTenant.Any(s =>
                s.ParentProfileId == parent.Id
                && (s.TenantId == tenantId
                    || _db.StudentSubscriptions.Any(sub => sub.StudentId == s.Id && sub.TenantId == tenantId)));
            if (!hasChildHere)
                throw new InvalidOperationException("Ce parent n'est pas lié à votre école.");
        }
    }

    private void EnsureCanAccessMailbox(string userId)
    {
        if (_tenantContext.HasTenant && _tenantContext.TenantId.HasValue)
            return;
        if (IsActiveGroupManager(userId) || IsActiveGroupMember(userId))
            return;
        RequireTenant();
    }

    private bool IsActiveGroupManager(string userId) =>
        _db.ExpertGroupManagerMandates.Any(m =>
            m.UserId == userId && m.Status == ExpertGroupManagerMandateStatus.Active);

    private bool IsActiveGroupMember(string userId) =>
        _db.ExpertGroupMembers.Any(m =>
            m.UserId == userId && m.Status == ExpertMembershipStatus.Active);

    private HashSet<Guid> GetManagedGroupIds(string managerUserId) =>
        _db.ExpertGroupManagerMandates
            .Where(m => m.UserId == managerUserId && m.Status == ExpertGroupManagerMandateStatus.Active)
            .Select(m => m.ExpertGroupId)
            .Distinct()
            .ToHashSet();

    private HashSet<string> GetManagedGroupMemberUserIds(string managerUserId)
    {
        var groupIds = GetManagedGroupIds(managerUserId);
        return _db.ExpertGroupMembers
            .Where(m => groupIds.Contains(m.ExpertGroupId)
                        && m.Status == ExpertMembershipStatus.Active
                        && m.UserId != managerUserId)
            .Select(m => m.UserId)
            .Distinct()
            .ToHashSet(StringComparer.Ordinal);
    }

    private HashSet<string> GetMyGroupManagerUserIds(string memberUserId)
    {
        var groupIds = GetMemberGroupIds(memberUserId);

        return _db.ExpertGroupManagerMandates
            .Where(m => groupIds.Contains(m.ExpertGroupId)
                        && m.Status == ExpertGroupManagerMandateStatus.Active
                        && m.UserId != memberUserId)
            .Select(m => m.UserId)
            .Distinct()
            .ToHashSet(StringComparer.Ordinal);
    }

    private HashSet<Guid> GetMemberGroupIds(string userId) =>
        _db.ExpertGroupMembers
            .Where(m => m.UserId == userId && m.Status == ExpertMembershipStatus.Active)
            .Select(m => m.ExpertGroupId)
            .Distinct()
            .ToHashSet();

    /// <summary>Autres membres actifs partageant au moins un groupe avec l'utilisateur.</summary>
    private HashSet<string> GetPeerGroupMemberUserIds(string userId)
    {
        var groupIds = GetMemberGroupIds(userId);
        if (groupIds.Count == 0)
            return new HashSet<string>(StringComparer.Ordinal);

        return _db.ExpertGroupMembers
            .Where(m => groupIds.Contains(m.ExpertGroupId)
                        && m.Status == ExpertMembershipStatus.Active
                        && m.UserId != userId)
            .Select(m => m.UserId)
            .Distinct()
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task EnsureGroupMessagingAllowedAsync(string senderId, string recipientId, CancellationToken ct)
    {
        var recipientRoles = await GetRolesAsync(recipientId);

        if (IsActiveGroupManager(senderId))
        {
            if (IsPlatformAdmin(recipientRoles))
                return;
            if (GetManagedGroupMemberUserIds(senderId).Contains(recipientId))
                return;
            // Un Responsable est aussi membre : autoriser les pairs du/des groupe(s).
            if (GetPeerGroupMemberUserIds(senderId).Contains(recipientId))
                return;
            throw new InvalidOperationException(
                "Le Responsable peut écrire aux membres de son groupe et à l'administration TutorSphere.");
        }

        if (IsActiveGroupMember(senderId))
        {
            if (GetMyGroupManagerUserIds(senderId).Contains(recipientId))
                return;
            if (GetPeerGroupMemberUserIds(senderId).Contains(recipientId))
                return;
            throw new InvalidOperationException(
                "Les membres peuvent écrire aux autres membres de leur groupe et au Responsable.");
        }

        await Task.CompletedTask;
        throw new InvalidOperationException("Messagerie de groupe non autorisée.");
    }

    private async Task<IReadOnlyList<MessageRecipientDto>> SearchForGroupManagerAsync(
        string managerUserId, string query, CancellationToken ct)
    {
        var results = new List<MessageRecipientDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var memberId in GetManagedGroupMemberUserIds(managerUserId))
        {
            var user = await _userManager.FindByIdAsync(memberId);
            if (user is null || !seen.Add(user.Id) || !MatchesQuery(user, query)) continue;
            results.Add(new MessageRecipientDto(
                user.Id,
                string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? user.Id) : user.FullName,
                user.Email,
                UserRoles.Expert));
        }

        foreach (var admin in await ListPlatformAdminsAsync())
        {
            if (!seen.Add(admin.Id) || !MatchesQuery(admin, query)) continue;
            results.Add(new MessageRecipientDto(
                admin.Id,
                string.IsNullOrWhiteSpace(admin.FullName) ? (admin.Email ?? admin.Id) : admin.FullName,
                admin.Email,
                UserRoles.PlatformAdmin));
        }

        await Task.CompletedTask;
        return results.OrderBy(r => r.DisplayName).Take(30).ToList();
    }

    private async Task<IReadOnlyList<MessageRecipientDto>> SearchForGroupMemberAsync(
        string memberUserId, string query, CancellationToken ct)
    {
        var results = new List<MessageRecipientDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var peerId in GetPeerGroupMemberUserIds(memberUserId))
        {
            var user = await _userManager.FindByIdAsync(peerId);
            if (user is null || !seen.Add(user.Id) || !MatchesQuery(user, query)) continue;
            var roles = await GetRolesAsync(user.Id);
            var role = IsActiveGroupManager(user.Id) ? UserRoles.GroupManager
                : roles.Contains(UserRoles.Expert) ? UserRoles.Expert
                : roles.FirstOrDefault() ?? "User";
            results.Add(new MessageRecipientDto(
                user.Id,
                string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? user.Id) : user.FullName,
                user.Email,
                role));
        }

        // Responsable éventuellement hors liste membres (sécurité / données partielles)
        foreach (var managerId in GetMyGroupManagerUserIds(memberUserId))
        {
            if (!seen.Add(managerId)) continue;
            var user = await _userManager.FindByIdAsync(managerId);
            if (user is null || !MatchesQuery(user, query)) continue;
            results.Add(new MessageRecipientDto(
                user.Id,
                string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? user.Id) : user.FullName,
                user.Email,
                UserRoles.GroupManager));
        }

        await Task.CompletedTask;
        return results.OrderBy(r => r.DisplayName).Take(30).ToList();
    }

    private async Task<List<ApplicationUser>> ListPlatformAdminsAsync()
    {
        var supers = await _userManager.GetUsersInRoleAsync(UserRoles.SuperAdmin);
        var platforms = await _userManager.GetUsersInRoleAsync(UserRoles.PlatformAdmin);
        return supers.Concat(platforms)
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .ToList();
    }

    private async Task<Guid> ResolveTenantForPartyAsync(string senderUserId, string recipientUserId, CancellationToken ct)
    {
        if (_tenantContext.HasTenant && _tenantContext.TenantId.HasValue)
            return _tenantContext.TenantId.Value;

        var sender = await _userManager.FindByIdAsync(senderUserId);
        if (sender?.TenantId is Guid sid && sid != Guid.Empty)
            return sid;

        var recipient = await _userManager.FindByIdAsync(recipientUserId);
        if (recipient is not null)
            return await ResolveTenantForAdminMessageAsync(recipient, ct);

        return await ResolveTenantForAdminMessageAsync(sender ?? throw new InvalidOperationException("Utilisateur introuvable."), ct);
    }

    private static bool IsPlatformAdmin(IEnumerable<string> roles) =>
        roles.Contains(UserRoles.SuperAdmin) || roles.Contains(UserRoles.PlatformAdmin);

    private async Task<IReadOnlyList<MessageRecipientDto>> SearchTeachersForLearnerAsync(
        string learnerUserId, string query, CancellationToken ct)
    {
        var teacherIds = await ResolveLinkedTeacherUserIdsAsync(learnerUserId, ct);
        var results = new List<MessageRecipientDto>();

        foreach (var teacherId in teacherIds)
        {
            var user = await _userManager.FindByIdAsync(teacherId);
            if (user is null) continue;
            if (!MatchesQuery(user, query)) continue;

            var roles = await GetRolesAsync(user.Id);
            results.Add(new MessageRecipientDto(
                user.Id,
                string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? user.Id) : user.FullName,
                user.Email,
                IsTeacher(roles) ? UserRoles.Tutor : roles.FirstOrDefault() ?? "User"));
        }

        return results
            .OrderBy(r => r.DisplayName)
            .Take(25)
            .ToList();
    }

    private async Task<IReadOnlyList<MessageRecipientDto>> SearchContactsForTeacherAsync(
        string query, CancellationToken ct)
    {
        var students = _db.Students
            .Where(s => s.IsActive && s.UserId != null && s.UserId != "")
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToList();

        var results = new List<MessageRecipientDto>();
        foreach (var s in students)
        {
            if (string.IsNullOrWhiteSpace(s.UserId)) continue;
            var display = $"{s.FirstName} {s.LastName}".Trim();
            if (!string.IsNullOrEmpty(query)
                && !display.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !(s.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                continue;

            results.Add(new MessageRecipientDto(
                s.UserId,
                string.IsNullOrWhiteSpace(display) ? s.UserId : display,
                s.Email,
                UserRoles.Student));
        }

        results.AddRange(SearchLinkedParentsForTeacher(query));

        await Task.CompletedTask;
        return results.Take(25).ToList();
    }

    /// <summary>
    /// Parents dont un enfant est inscrit aux cours de l'enseignant. Les coordonnées ne sont pas
    /// exposées : l'échange passe uniquement par la messagerie interne.
    /// </summary>
    private List<MessageRecipientDto> SearchLinkedParentsForTeacher(string query)
    {
        var studentIds = _db.Students.Select(s => s.Id).ToHashSet();
        foreach (var id in _db.StudentSubscriptions.Select(s => s.StudentId).Distinct().ToList())
            studentIds.Add(id);
        if (studentIds.Count == 0)
            return [];

        var parentIds = _db.StudentsForAnyTenant
            .Where(s => studentIds.Contains(s.Id) && s.ParentProfileId != null)
            .Select(s => s.ParentProfileId!.Value)
            .Distinct()
            .ToList();
        if (parentIds.Count == 0)
            return [];

        return _db.ParentProfilesForAnyTenant
            .Where(p => parentIds.Contains(p.Id) && p.UserId != null && p.UserId != "")
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToList()
            .Select(p => new
            {
                p.UserId,
                Name = $"{p.FirstName} {p.LastName}".Trim()
            })
            .Where(p => string.IsNullOrEmpty(query)
                        || p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(p => new MessageRecipientDto(
                p.UserId!,
                string.IsNullOrWhiteSpace(p.Name) ? "Parent" : p.Name,
                null,
                UserRoles.Parent))
            .ToList();
    }

    private async Task<HashSet<string>> ResolveLinkedTeacherUserIdsAsync(string learnerUserId, CancellationToken ct)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        var student = _db.StudentsForAnyTenant.FirstOrDefault(s => s.UserId == learnerUserId);
        if (student is not null)
        {
            var tenantIds = new HashSet<Guid> { student.TenantId };
            foreach (var tid in _db.StudentSubscriptionsForAnyTenant
                         .Where(s => s.StudentId == student.Id)
                         .Select(s => s.TenantId)
                         .Distinct())
                tenantIds.Add(tid);

            foreach (var ownerId in _db.Tenants
                         .Where(t => tenantIds.Contains(t.Id) && t.OwnerUserId != null && t.OwnerUserId != "")
                         .Select(t => t.OwnerUserId!))
                ids.Add(ownerId);
        }

        var parent = _db.ParentProfilesForAnyTenant.FirstOrDefault(p => p.UserId == learnerUserId);
        if (parent is not null)
        {
            var childTenantIds = _db.StudentsForAnyTenant
                .Where(s => s.ParentProfileId == parent.Id)
                .Select(s => s.TenantId)
                .Distinct()
                .ToHashSet();

            foreach (var subTid in _db.StudentSubscriptionsForAnyTenant
                         .Where(s => _db.StudentsForAnyTenant.Any(st => st.Id == s.StudentId && st.ParentProfileId == parent.Id))
                         .Select(s => s.TenantId)
                         .Distinct())
                childTenantIds.Add(subTid);

            foreach (var ownerId in _db.Tenants
                         .Where(t => childTenantIds.Contains(t.Id) && t.OwnerUserId != null && t.OwnerUserId != "")
                         .Select(t => t.OwnerUserId!))
                ids.Add(ownerId);
        }

        await Task.CompletedTask;
        return ids;
    }

    private static bool MatchesQuery(ApplicationUser user, string query)
    {
        if (string.IsNullOrEmpty(query)) return true;
        return (user.FullName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
               || (user.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private async Task<IList<string>> GetRolesAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");
        return await _userManager.GetRolesAsync(user);
    }

    private static bool IsStudent(IEnumerable<string> roles) =>
        roles.Contains(UserRoles.Student);

    private static bool IsTeacher(IEnumerable<string> roles) =>
        roles.Contains(UserRoles.Tutor) || roles.Contains(UserRoles.TeachingAssistant);

    private static bool IsParent(IEnumerable<string> roles) =>
        roles.Contains(UserRoles.Parent);

    private async Task<Guid> ResolveTenantForAdminMessageAsync(ApplicationUser recipient, CancellationToken ct)
    {
        if (recipient.TenantId is Guid tid && tid != Guid.Empty)
            return tid;

        var owned = _db.Tenants.FirstOrDefault(t => t.OwnerUserId == recipient.Id);
        if (owned is not null)
            return owned.Id;

        var holding = _db.Tenants.FirstOrDefault(t =>
            t.Slug == "platform-parents" || t.Slug == "tutorsphere-parents");
        if (holding is not null)
            return holding.Id;

        var any = _db.Tenants.OrderBy(t => t.CreatedAt).FirstOrDefault();
        if (any is not null)
            return any.Id;

        await Task.CompletedTask;
        throw new InvalidOperationException(
            "Impossible d'attribuer un espace messagerie (aucun profil / tenant disponible).");
    }

    private Guid RequireTenant()
    {
        if (!_tenantContext.HasTenant || !_tenantContext.TenantId.HasValue)
            throw new InvalidOperationException("Un contexte locataire (tenant) est requis pour la messagerie.");

        return _tenantContext.TenantId.Value;
    }

    private async Task EnsureUserInTenantAsync(string userId, Guid tenantId, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        if (!user.TenantId.HasValue || user.TenantId.Value != tenantId)
            throw new InvalidOperationException("L'utilisateur n'appartient pas à ce locataire.");
    }

    private static MessageDto MapToDto(Message message) => new(
        message.Id,
        message.SenderUserId,
        message.RecipientUserId,
        message.Subject,
        message.Body,
        message.IsRead,
        message.ReadAt,
        message.CreatedAt);
}
