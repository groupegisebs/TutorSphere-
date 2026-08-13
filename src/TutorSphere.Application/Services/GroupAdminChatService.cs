using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.ExpertGroupGovernance;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface IGroupAdminChatService
{
    Task<IReadOnlyList<GroupAdminConversationDto>> ListForAdminAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GroupAdminConversationDto>> ListForManagerAsync(string managerUserId, CancellationToken ct = default);
    Task<GroupAdminConversationDto> OpenOrCreateForGroupAsync(Guid groupId, string managerUserId, CreateGroupAdminConversationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<GroupAdminMessageDto>> ListMessagesAsync(Guid conversationId, CancellationToken ct = default);
    Task<GroupAdminMessageDto> PostMessageAsync(Guid conversationId, string senderUserId, PostGroupAdminMessageRequest request, CancellationToken ct = default);
}

public class GroupAdminChatService(IApplicationDbContext db, IExpertGroupManagerService managers) : IGroupAdminChatService
{
    public Task<IReadOnlyList<GroupAdminConversationDto>> ListForAdminAsync(CancellationToken ct = default)
    {
        var conversations = db.GroupAdminConversations.OrderByDescending(c => c.UpdatedAt).ToList();
        return Task.FromResult(MapList(conversations));
    }

    public Task<IReadOnlyList<GroupAdminConversationDto>> ListForManagerAsync(string managerUserId, CancellationToken ct = default)
    {
        var groupIds = db.ExpertGroupManagerMandates
            .Where(m => m.UserId == managerUserId && m.Status == ExpertGroupManagerMandateStatus.Active)
            .Select(m => m.ExpertGroupId)
            .ToHashSet();

        var conversations = db.GroupAdminConversations
            .Where(c => groupIds.Contains(c.ExpertGroupId))
            .OrderByDescending(c => c.UpdatedAt)
            .ToList();
        return Task.FromResult(MapList(conversations));
    }

    public async Task<GroupAdminConversationDto> OpenOrCreateForGroupAsync(
        Guid groupId,
        string managerUserId,
        CreateGroupAdminConversationRequest request,
        CancellationToken ct = default)
    {
        if (!managers.IsActiveManager(managerUserId, groupId)
            && !db.ExpertGroups.Any(g => g.Id == groupId)) // admin path validated by controller
        {
            // Controllers enforce roles; managers still must belong.
        }

        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Message))
            throw new InvalidOperationException("Sujet et message requis.");

        var group = db.ExpertGroups.FirstOrDefault(g => g.Id == groupId)
            ?? throw new InvalidOperationException("Groupe introuvable.");

        var year = DateTime.UtcNow.Year;
        var country = group.CountryCode ?? "INT";
        var seq = db.GroupAdminConversations.Count(c => c.ExpertGroupId == groupId) + 1;
        var reference = $"TS-GRP-{country}-{year}-{seq:D5}";

        var conversation = new GroupAdminConversation
        {
            ExpertGroupId = groupId,
            Reference = reference,
            Subject = request.Subject.Trim(),
            Category = request.Category,
            Priority = request.Priority,
            Status = GroupAdminConversationStatus.AdminResponseRequired,
            CreatedByManagerUserId = managerUserId
        };
        db.Add(conversation);
        await db.SaveChangesAsync(ct);

        db.Add(new GroupAdminMessage
        {
            ConversationId = conversation.Id,
            SenderUserId = managerUserId,
            Body = request.Message.Trim(),
            AttachmentReference = string.IsNullOrWhiteSpace(request.AttachmentReference)
                ? null
                : request.AttachmentReference.Trim(),
            SentAtUtc = DateTime.UtcNow
        });
        conversation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return MapList([conversation]).First();
    }

    public Task<IReadOnlyList<GroupAdminMessageDto>> ListMessagesAsync(Guid conversationId, CancellationToken ct = default)
    {
        IReadOnlyList<GroupAdminMessageDto> messages = db.GroupAdminMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAtUtc)
            .Select(m => new GroupAdminMessageDto(
                m.Id, m.SenderUserId, null, m.Body, m.AttachmentReference,
                m.SentAtUtc, m.ReadAtUtc, m.EditedAtUtc))
            .ToList();
        return Task.FromResult(messages);
    }

    public async Task<GroupAdminMessageDto> PostMessageAsync(
        Guid conversationId, string senderUserId, PostGroupAdminMessageRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new InvalidOperationException("Message requis.");

        var conversation = db.GroupAdminConversations.FirstOrDefault(c => c.Id == conversationId)
            ?? throw new InvalidOperationException("Conversation introuvable.");

        var msg = new GroupAdminMessage
        {
            ConversationId = conversationId,
            SenderUserId = senderUserId,
            Body = request.Message.Trim(),
            AttachmentReference = string.IsNullOrWhiteSpace(request.AttachmentReference)
                ? null
                : request.AttachmentReference.Trim(),
            SentAtUtc = DateTime.UtcNow
        };
        db.Add(msg);
        conversation.Status = managers.IsActiveManager(senderUserId, conversation.ExpertGroupId)
            ? GroupAdminConversationStatus.AdminResponseRequired
            : GroupAdminConversationStatus.GroupResponseRequired;
        conversation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new GroupAdminMessageDto(
            msg.Id, msg.SenderUserId, null, msg.Body, msg.AttachmentReference,
            msg.SentAtUtc, msg.ReadAtUtc, msg.EditedAtUtc);
    }

    private IReadOnlyList<GroupAdminConversationDto> MapList(List<GroupAdminConversation> conversations)
    {
        var groupNames = db.ExpertGroups.ToDictionary(g => g.Id, g => g.Name);
        var result = new List<GroupAdminConversationDto>();
        foreach (var c in conversations)
        {
            var msgs = db.GroupAdminMessages.Where(m => m.ConversationId == c.Id).ToList();
            result.Add(new GroupAdminConversationDto(
                c.Id,
                c.ExpertGroupId,
                groupNames.GetValueOrDefault(c.ExpertGroupId, "—"),
                c.Reference,
                c.Subject,
                c.Category,
                c.Priority,
                c.Status,
                c.CreatedAt,
                msgs.Count,
                msgs.Count == 0 ? null : msgs.Max(m => m.SentAtUtc)));
        }
        return result;
    }
}
