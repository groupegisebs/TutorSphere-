using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;

namespace TutorSphere.Infrastructure.Services;

public interface IAdminMailboxService
{
    Task<MailboxFolderCountsDto> GetCountsAsync(string adminUserId, CancellationToken ct = default);
    Task<IReadOnlyList<MailboxMessageListItemDto>> ListAsync(
        string adminUserId, MailboxFolder folder, string? search, CancellationToken ct = default);
    Task<MailboxMessageDetailDto?> GetAsync(string adminUserId, Guid messageId, CancellationToken ct = default);
    Task<AdminMailboxSendResultDto> ComposeAsync(
        string adminUserId, AdminComposeMessageRequest request, CancellationToken ct = default);
    Task<MailboxMessageDetailDto?> StarAsync(string adminUserId, Guid messageId, bool starred, CancellationToken ct = default);
    Task<MailboxMessageDetailDto?> ArchiveAsync(string adminUserId, Guid messageId, CancellationToken ct = default);
    Task<MailboxMessageDetailDto?> TrashAsync(string adminUserId, Guid messageId, CancellationToken ct = default);
    Task<bool> DeletePermanentAsync(string adminUserId, Guid messageId, CancellationToken ct = default);
}

public sealed class AdminMailboxService(
    IApplicationDbContext db,
    UserManager<ApplicationUser> users,
    IEmailService email,
    IConfiguration config,
    IRealTimeMessaging realtime) : IAdminMailboxService
{
    public async Task<MailboxFolderCountsDto> GetCountsAsync(string adminUserId, CancellationToken ct = default)
    {
        var all = await RelevantAsync(adminUserId, ct);
        return new MailboxFolderCountsDto(
            Inbox: CountFolder(all, adminUserId, MailboxFolder.Inbox),
            Sent: CountFolder(all, adminUserId, MailboxFolder.Sent),
            Drafts: CountFolder(all, adminUserId, MailboxFolder.Drafts),
            Archive: CountFolder(all, adminUserId, MailboxFolder.Archive),
            Trash: CountFolder(all, adminUserId, MailboxFolder.Trash),
            Starred: CountFolder(all, adminUserId, MailboxFolder.Starred),
            UnreadInbox: all.Count(m =>
                m.RecipientUserId == adminUserId
                && !m.RecipientDeleted
                && !m.RecipientArchived
                && !m.IsDraft
                && !m.IsRead));
    }

    public async Task<IReadOnlyList<MailboxMessageListItemDto>> ListAsync(
        string adminUserId, MailboxFolder folder, string? search, CancellationToken ct = default)
    {
        var q = (await RelevantAsync(adminUserId, ct))
            .Where(m => InFolder(m, adminUserId, folder))
            .OrderByDescending(m => m.CreatedAt);

        var s = (search ?? "").Trim();
        var list = new List<MailboxMessageListItemDto>();
        foreach (var m in q)
        {
            var item = await MapListItemAsync(m, adminUserId, ct);
            if (s.Length > 0
                && !item.Subject.Contains(s, StringComparison.OrdinalIgnoreCase)
                && !item.Preview.Contains(s, StringComparison.OrdinalIgnoreCase)
                && !item.CounterpartName.Contains(s, StringComparison.OrdinalIgnoreCase)
                && !(item.CounterpartEmail?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false))
                continue;
            list.Add(item);
        }

        return list.Take(200).ToList();
    }

    public async Task<MailboxMessageDetailDto?> GetAsync(
        string adminUserId, Guid messageId, CancellationToken ct = default)
    {
        var m = await db.Messages.FirstOrDefaultAsync(x => x.Id == messageId, ct);
        if (m is null || !IsParticipant(m, adminUserId))
            return null;

        if (m.RecipientUserId == adminUserId && !m.IsRead && !m.IsDraft)
        {
            m.IsRead = true;
            m.ReadAt = DateTime.UtcNow;
            m.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return await MapDetailAsync(m, adminUserId, ct);
    }

    public async Task<AdminMailboxSendResultDto> ComposeAsync(
        string adminUserId, AdminComposeMessageRequest request, CancellationToken ct = default)
    {
        var subject = (request.Subject ?? "").Trim();
        var body = (request.Body ?? "").Trim();
        if (string.IsNullOrWhiteSpace(subject))
            throw new InvalidOperationException("L'objet est obligatoire.");
        if (string.IsNullOrWhiteSpace(body) && !request.SaveAsDraft)
            throw new InvalidOperationException("Le message est obligatoire.");

        var recipientUserId = (request.RecipientUserId ?? "").Trim();
        var external = NormalizeEmail(request.ExternalEmail);

        ApplicationUser? recipient = null;
        if (!string.IsNullOrEmpty(recipientUserId))
        {
            recipient = await users.FindByIdAsync(recipientUserId)
                ?? throw new InvalidOperationException("Destinataire introuvable.");
            if (recipient.Id == adminUserId)
                throw new InvalidOperationException("Impossible d'envoyer un message à vous-même.");
            external ??= NormalizeEmail(recipient.Email);
        }

        if (recipient is null && string.IsNullOrEmpty(external) && !request.SaveAsDraft)
            throw new InvalidOperationException("Indiquez un destinataire plateforme ou une adresse e-mail.");

        var tenantId = recipient is not null
            ? await ResolveTenantAsync(recipient, ct)
            : await ResolveAdminTenantAsync(adminUserId, ct);

        var message = new Message
        {
            TenantId = tenantId,
            SenderUserId = adminUserId,
            RecipientUserId = recipient?.Id ?? "",
            ExternalRecipientEmail = external,
            Subject = subject,
            Body = body,
            IsDraft = request.SaveAsDraft,
            InReplyToMessageId = request.InReplyToMessageId,
            IsRead = request.SaveAsDraft // drafts don't need unread for sender
        };
        db.Add(message);
        await db.SaveChangesAsync(ct);

        var emailSent = false;
        string? emailError = null;

        if (!request.SaveAsDraft)
        {
            if (recipient is not null)
            {
                try
                {
                    await realtime.NotifyMessageReceivedAsync(
                        recipient.Id,
                        new MessageDto(
                            message.Id, message.SenderUserId, message.RecipientUserId,
                            message.Subject, message.Body, message.IsRead, message.ReadAt, message.CreatedAt),
                        ct);
                }
                catch { /* non-blocking */ }
            }

            if (request.SendEmailCopy && !string.IsNullOrEmpty(external))
            {
                try
                {
                    var admin = await users.FindByIdAsync(adminUserId);
                    var webBase = (config["WebBaseUrl"] ?? "https://tutorsphere.gisebs.com").TrimEnd('/');
                    var inboxPath = recipient is null ? "/login" : await ResolveInboxPathAsync(recipient);
                    var firstName = recipient?.FirstName;
                    if (string.IsNullOrWhiteSpace(firstName))
                        firstName = external.Split('@')[0];

                    await email.SendAdminDirectMessageAsync(
                        external,
                        firstName!,
                        admin?.FullName ?? "Administration TutorSphere",
                        subject,
                        body,
                        $"{webBase}{inboxPath}",
                        ct);

                    emailSent = true;
                    message.EmailSent = true;
                    message.EmailSentAt = DateTime.UtcNow;
                    message.EmailError = null;
                }
                catch (Exception ex)
                {
                    emailError = ex.Message;
                    message.EmailSent = false;
                    message.EmailError = ex.Message;
                }

                message.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        var detail = await MapDetailAsync(message, adminUserId, ct)
            ?? throw new InvalidOperationException("Message introuvable après envoi.");
        return new AdminMailboxSendResultDto(detail, emailSent, emailError);
    }

    public async Task<MailboxMessageDetailDto?> StarAsync(
        string adminUserId, Guid messageId, bool starred, CancellationToken ct = default)
    {
        var m = await db.Messages.FirstOrDefaultAsync(x => x.Id == messageId, ct);
        if (m is null || !IsParticipant(m, adminUserId)) return null;
        m.IsStarred = starred;
        m.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await MapDetailAsync(m, adminUserId, ct);
    }

    public async Task<MailboxMessageDetailDto?> ArchiveAsync(
        string adminUserId, Guid messageId, CancellationToken ct = default)
    {
        var m = await db.Messages.FirstOrDefaultAsync(x => x.Id == messageId, ct);
        if (m is null || !IsParticipant(m, adminUserId)) return null;
        if (m.RecipientUserId == adminUserId) m.RecipientArchived = true;
        if (m.SenderUserId == adminUserId) m.SenderArchived = true;
        m.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await MapDetailAsync(m, adminUserId, ct);
    }

    public async Task<MailboxMessageDetailDto?> TrashAsync(
        string adminUserId, Guid messageId, CancellationToken ct = default)
    {
        var m = await db.Messages.FirstOrDefaultAsync(x => x.Id == messageId, ct);
        if (m is null || !IsParticipant(m, adminUserId)) return null;
        if (m.RecipientUserId == adminUserId) m.RecipientDeleted = true;
        if (m.SenderUserId == adminUserId) m.SenderDeleted = true;
        m.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await MapDetailAsync(m, adminUserId, ct);
    }

    public async Task<bool> DeletePermanentAsync(string adminUserId, Guid messageId, CancellationToken ct = default)
    {
        var m = await db.Messages.FirstOrDefaultAsync(x => x.Id == messageId, ct);
        if (m is null || !IsParticipant(m, adminUserId)) return false;
        if (!(m.SenderDeleted || m.RecipientDeleted || m.IsDraft))
            throw new InvalidOperationException("Passez d'abord le message à la corbeille.");
        db.Remove(m);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<List<Message>> RelevantAsync(string adminUserId, CancellationToken ct) =>
        await db.Messages
            .Where(m => m.SenderUserId == adminUserId || m.RecipientUserId == adminUserId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

    private static bool IsParticipant(Message m, string userId) =>
        m.SenderUserId == userId || m.RecipientUserId == userId;

    private static int CountFolder(IEnumerable<Message> all, string userId, MailboxFolder folder) =>
        all.Count(m => InFolder(m, userId, folder));

    private static bool InFolder(Message m, string userId, MailboxFolder folder) => folder switch
    {
        MailboxFolder.Inbox =>
            m.RecipientUserId == userId && !m.RecipientDeleted && !m.RecipientArchived && !m.IsDraft,
        MailboxFolder.Sent =>
            m.SenderUserId == userId && !m.SenderDeleted && !m.IsDraft && !m.SenderArchived,
        MailboxFolder.Drafts =>
            m.SenderUserId == userId && m.IsDraft && !m.SenderDeleted,
        MailboxFolder.Archive =>
            ((m.RecipientUserId == userId && m.RecipientArchived && !m.RecipientDeleted)
             || (m.SenderUserId == userId && m.SenderArchived && !m.SenderDeleted))
            && !m.IsDraft,
        MailboxFolder.Trash =>
            (m.RecipientUserId == userId && m.RecipientDeleted)
            || (m.SenderUserId == userId && m.SenderDeleted),
        MailboxFolder.Starred =>
            m.IsStarred
            && ((m.RecipientUserId == userId && !m.RecipientDeleted)
                || (m.SenderUserId == userId && !m.SenderDeleted)),
        _ => false
    };

    private static MailboxFolder ResolveFolder(Message m, string userId)
    {
        if (InFolder(m, userId, MailboxFolder.Trash)) return MailboxFolder.Trash;
        if (InFolder(m, userId, MailboxFolder.Drafts)) return MailboxFolder.Drafts;
        if (InFolder(m, userId, MailboxFolder.Archive)) return MailboxFolder.Archive;
        if (InFolder(m, userId, MailboxFolder.Inbox)) return MailboxFolder.Inbox;
        if (InFolder(m, userId, MailboxFolder.Sent)) return MailboxFolder.Sent;
        return MailboxFolder.Inbox;
    }

    private async Task<MailboxMessageListItemDto> MapListItemAsync(Message m, string adminUserId, CancellationToken ct)
    {
        var outbound = m.SenderUserId == adminUserId;
        var counterpartId = outbound ? m.RecipientUserId : m.SenderUserId;
        ApplicationUser? counterpart = null;
        if (!string.IsNullOrEmpty(counterpartId))
            counterpart = await users.FindByIdAsync(counterpartId);

        var name = counterpart?.FullName
                   ?? m.ExternalRecipientEmail
                   ?? counterpartId
                   ?? "—";
        var emailAddr = counterpart?.Email ?? m.ExternalRecipientEmail;
        var role = counterpart is null ? "Externe" : await PrimaryRoleAsync(counterpart);

        var preview = m.Body.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        if (preview.Length > 110) preview = preview[..110] + "…";

        return new MailboxMessageListItemDto(
            m.Id,
            m.Subject,
            preview,
            name,
            emailAddr,
            role,
            counterpartId ?? "",
            outbound,
            m.IsRead || outbound,
            m.IsStarred,
            m.EmailSent,
            m.CreatedAt,
            !string.IsNullOrWhiteSpace(m.ExternalRecipientEmail));
    }

    private async Task<MailboxMessageDetailDto?> MapDetailAsync(Message m, string adminUserId, CancellationToken ct)
    {
        var sender = await users.FindByIdAsync(m.SenderUserId);
        ApplicationUser? recipient = null;
        if (!string.IsNullOrEmpty(m.RecipientUserId))
            recipient = await users.FindByIdAsync(m.RecipientUserId);

        var outbound = m.SenderUserId == adminUserId;
        return new MailboxMessageDetailDto(
            m.Id,
            m.Subject,
            m.Body,
            m.SenderUserId,
            sender?.FullName ?? m.SenderUserId,
            sender?.Email,
            m.RecipientUserId,
            recipient?.FullName ?? m.ExternalRecipientEmail ?? "—",
            recipient?.Email ?? m.ExternalRecipientEmail,
            recipient is null ? (string.IsNullOrEmpty(m.ExternalRecipientEmail) ? null : "Externe") : await PrimaryRoleAsync(recipient),
            m.ExternalRecipientEmail,
            outbound,
            m.IsRead,
            m.IsStarred,
            m.IsDraft,
            m.EmailSent,
            m.EmailError,
            m.EmailSentAt,
            m.CreatedAt,
            m.InReplyToMessageId,
            ResolveFolder(m, adminUserId));
    }

    private async Task<string> PrimaryRoleAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        if (roles.Contains(UserRoles.Parent)) return "Parent";
        if (roles.Contains(UserRoles.Student)) return "Élève";
        if (roles.Contains(UserRoles.Tutor) || roles.Contains(UserRoles.TeachingAssistant)) return "Tuteur";
        if (roles.Contains(UserRoles.Expert)) return "Expert";
        if (roles.Contains(UserRoles.GroupManager)) return "Responsable";
        if (roles.Contains(UserRoles.PlatformAdmin)) return "Admin plateforme";
        if (roles.Contains(UserRoles.SuperAdmin)) return "Super admin";
        return roles.FirstOrDefault() ?? "Utilisateur";
    }

    private async Task<string> ResolveInboxPathAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        if (roles.Contains(UserRoles.Parent)) return "/parent/messages";
        if (roles.Contains(UserRoles.Student)) return "/student/messages";
        if (roles.Contains(UserRoles.Tutor) || roles.Contains(UserRoles.TeachingAssistant))
            return "/tutor/messages";
        if (roles.Contains(UserRoles.Expert) || roles.Contains(UserRoles.GroupManager))
            return "/expert/messages";
        return "/login";
    }

    private async Task<Guid> ResolveTenantAsync(ApplicationUser recipient, CancellationToken ct)
    {
        if (recipient.TenantId is Guid tid && tid != Guid.Empty)
            return tid;

        var owned = await db.Tenants.FirstOrDefaultAsync(t => t.OwnerUserId == recipient.Id, ct);
        if (owned is not null) return owned.Id;

        var student = db.StudentsForAnyTenant.FirstOrDefault(s => s.UserId == recipient.Id);
        if (student is not null) return student.TenantId;

        return await ResolveAdminTenantAsync(recipient.Id, ct);
    }

    private async Task<Guid> ResolveAdminTenantAsync(string userId, CancellationToken ct)
    {
        var any = await db.Tenants.OrderBy(t => t.CreatedAt).Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (any != Guid.Empty) return any;
        throw new InvalidOperationException("Aucun tenant disponible pour stocker le message.");
    }

    private static string? NormalizeEmail(string? email)
    {
        var e = (email ?? "").Trim();
        return string.IsNullOrWhiteSpace(e) ? null : e.ToLowerInvariant();
    }
}
