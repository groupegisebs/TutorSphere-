using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Domain.Entities;
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
        var tenantId = RequireTenant();
        await EnsureUserInTenantAsync(senderUserId, tenantId, ct);
        await EnsureUserInTenantAsync(request.RecipientUserId, tenantId, ct);

        if (senderUserId == request.RecipientUserId)
            throw new InvalidOperationException("Impossible d'envoyer un message à vous-même.");

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

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(string userId, CancellationToken ct = default)
    {
        RequireTenant();

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
                user?.FullName ?? group.Key,
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
        RequireTenant();

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
        RequireTenant();

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
        RequireTenant();

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
        // Sans tenant admin : le filtre ITenantEntity laisse passer toutes les lignes.
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
