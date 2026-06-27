using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TutorSphere.Application.Common;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Messages;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.MultiTenancy;
using TutorSphere.Infrastructure.Persistence;

namespace TutorSphere.Api.Hubs;

[Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.Parent},{UserRoles.Student},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
public class MessagesHub : Hub
{
    private readonly IMessageService _messageService;

    public MessagesHub(IMessageService messageService) => _messageService = messageService;

    public async Task SendMessage(SendMessageRequest request)
    {
        var senderUserId = GetUserId();
        var message = await _messageService.SendAsync(senderUserId, request, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("MessageSent", message);
    }

    public async Task MarkConversationRead(string otherUserId)
    {
        var userId = GetUserId();
        await _messageService.MarkConversationAsReadAsync(userId, otherUserId, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("ConversationMarkedRead", otherUserId);
    }

    private string GetUserId() =>
        Context.User?.GetUserId()
        ?? throw new HubException("Utilisateur non authentifié.");
}

public class NameIdentifierUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.GetUserId();
}

public class TenantHubFilter : IHubFilter
{
    private readonly TenantContext _tenantContext;
    private readonly ApplicationDbContext _db;

    public TenantHubFilter(TenantContext tenantContext, ApplicationDbContext db)
    {
        _tenantContext = tenantContext;
        _db = db;
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        await SetTenantAsync(context.Context);
        try
        {
            await next(context);
        }
        finally
        {
            _tenantContext.Clear();
        }
    }

    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Task> next)
    {
        await SetTenantAsync(context.Context);
        try
        {
            await next(context);
        }
        finally
        {
            _tenantContext.Clear();
        }
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        await SetTenantAsync(invocationContext.Context);
        try
        {
            return await next(invocationContext);
        }
        finally
        {
            _tenantContext.Clear();
        }
    }

    private async Task SetTenantAsync(HubCallerContext context)
    {
        _tenantContext.Clear();

        var httpContext = context.GetHttpContext();
        if (httpContext?.Request.Headers.TryGetValue("X-Tenant-Slug", out var slugHeader) == true
            && !string.IsNullOrWhiteSpace(slugHeader))
        {
            var slug = slugHeader.ToString().ToLowerInvariant();
            var tenant = await _db.TenantsSet
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug || t.Subdomain == slug);

            if (tenant is not null)
            {
                ValidateTenantClaim(context, tenant.Id);
                _tenantContext.SetTenant(tenant.Id, tenant.Slug);
                return;
            }
        }

        var tenantIdClaim = context.User?.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantIdClaim, out var tenantId))
            _tenantContext.SetTenant(tenantId);
    }

    private static void ValidateTenantClaim(HubCallerContext context, Guid resolvedTenantId)
    {
        var tenantIdClaim = context.User?.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantIdClaim, out var claimTenantId) && claimTenantId != resolvedTenantId)
            throw new HubException("Le locataire ne correspond pas au jeton d'authentification.");
    }
}
