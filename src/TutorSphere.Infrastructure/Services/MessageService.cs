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
