using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorSphere.Application.DTOs.Settings;
using TutorSphere.Application.Services;
using TutorSphere.Domain.Enums;
using TutorSphere.Infrastructure.Identity;
using TutorSphere.Infrastructure.MultiTenancy;
using TutorSphere.Infrastructure.Persistence;

namespace TutorSphere.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ILessonService _lessonService;
    private readonly TenantContext _tenantContext;
    private readonly IConfiguration _configuration;

    public MeController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ILessonService lessonService,
        TenantContext tenantContext,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _db = db;
        _lessonService = lessonService;
        _tenantContext = tenantContext;
        _configuration = configuration;
    }

    [HttpGet("notification-preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> GetNotificationPreferences()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();
        return Ok(new NotificationPreferencesDto(user.EmailLessonReminders));
    }

    [HttpPut("notification-preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> UpdateNotificationPreferences(
        [FromBody] UpdateNotificationPreferencesRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        user.EmailLessonReminders = request.EmailLessonReminders;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });

        return Ok(new NotificationPreferencesDto(user.EmailLessonReminders));
    }

    [HttpGet("calendar-feed")]
    [Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<CalendarFeedDto>> GetCalendarFeed()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();
        return Ok(BuildFeedDto(user));
    }

    [HttpPost("calendar-feed/regenerate")]
    [Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<CalendarFeedDto>> RegenerateCalendarFeed()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        user.CalendarFeedToken = Convert.ToHexString(Guid.NewGuid().ToByteArray())
            + Convert.ToHexString(Guid.NewGuid().ToByteArray());
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });

        return Ok(BuildFeedDto(user));
    }

    [HttpDelete("calendar-feed")]
    [Authorize(Roles = $"{UserRoles.Tutor},{UserRoles.TeachingAssistant},{UserRoles.SuperAdmin}")]
    public async Task<ActionResult<CalendarFeedDto>> DisableCalendarFeed()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        user.CalendarFeedToken = null;
        await _userManager.UpdateAsync(user);
        return Ok(BuildFeedDto(user));
    }

    /// <summary>Public ICS feed for Google Calendar, Outlook, Apple Calendar, etc.</summary>
    [HttpGet("/api/calendar/feed/{token}.ics")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadCalendarFeed(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 16)
            return NotFound();

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.CalendarFeedToken == token, ct);
        if (user is null || user.TenantId is null)
            return NotFound();

        _tenantContext.SetTenant(user.TenantId.Value);

        var start = DateTime.UtcNow.AddMonths(-1);
        var end = DateTime.UtcNow.AddMonths(6);
        var lessons = await _lessonService.GetByDateRangeAsync(start, end, ct);

        var schoolName = await _db.TenantsSet.AsNoTracking()
            .Where(t => t.Id == user.TenantId.Value)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct) ?? "TutorSphere";

        var ics = IcsCalendarBuilder.Build(schoolName, lessons);
        return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", "tutorsphere.ics");
    }

    private CalendarFeedDto BuildFeedDto(ApplicationUser user)
    {
        var enabled = !string.IsNullOrWhiteSpace(user.CalendarFeedToken);
        string? feedUrl = null;
        string? webcalUrl = null;

        if (enabled)
        {
            var apiBase = (_configuration["ApiPublicBaseUrl"]
                ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');
            feedUrl = $"{apiBase}/api/calendar/feed/{user.CalendarFeedToken}.ics";
            webcalUrl = feedUrl.Replace("https://", "webcal://", StringComparison.OrdinalIgnoreCase)
                .Replace("http://", "webcal://", StringComparison.OrdinalIgnoreCase);
        }

        return new CalendarFeedDto(
            enabled,
            feedUrl,
            webcalUrl,
            "Ajoutez cette URL dans Google Agenda (Autres agendas → À partir d’une URL), Outlook (Ajouter un calendrier → À partir d’Internet) ou Apple Calendar (Fichier → Nouvel abonnement).");
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId)) return null;
        return await _userManager.FindByIdAsync(userId);
    }
}
