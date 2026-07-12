using TutorSphere.Application.DTOs.Lessons;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Web.Services;

public sealed class LessonService
{
    private readonly ApiClient _api;

    public LessonService(ApiClient api) => _api = api;

    /// <summary>Returns lessons for a ±60 / +180-day window around today.</summary>
    public async Task<List<LessonDto>> GetLessonsAsync()
    {
        var start = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-60).ToString("O"));
        var end   = Uri.EscapeDataString(DateTime.UtcNow.AddDays(180).ToString("O"));
        return await _api.GetAsync<List<LessonDto>>($"api/lessons?start={start}&end={end}") ?? [];
    }

    public async Task<List<LessonDto>> CreateLessonAsync(
        string title, string? description, string? subject,
        DateTime startTime, DateTime endTime,
        string modeDisplay,
        string? location = null, string? meetingUrl = null, string? sessionNotes = null,
        string? recurrenceFrequency = null,
        int? recurrenceOccurrences = null,
        DateTime? recurrenceUntil = null)
    {
        var mode = MapMode(modeDisplay);
        var req = new CreateLessonRequest(
            title, description, subject, startTime, endTime,
            mode, location, meetingUrl, sessionNotes,
            StudentIds: null,
            RecurrenceFrequency: recurrenceFrequency,
            RecurrenceOccurrences: recurrenceOccurrences,
            RecurrenceUntil: recurrenceUntil);
        return await _api.PostAsync<List<LessonDto>>("api/lessons", req) ?? [];
    }

    public async Task<LessonDto?> UpdateLessonAsync(
        Guid id, string title, string? description, string? subject,
        DateTime startTime, DateTime endTime,
        string modeDisplay,
        string? location = null, string? meetingUrl = null, string? sessionNotes = null)
    {
        var result = await UpdateLessonWithErrorAsync(
            id, title, description, subject, startTime, endTime, modeDisplay, location, meetingUrl, sessionNotes);
        return result.Value;
    }

    public async Task<ApiResult<LessonDto>> UpdateLessonWithErrorAsync(
        Guid id, string title, string? description, string? subject,
        DateTime startTime, DateTime endTime,
        string modeDisplay,
        string? location = null, string? meetingUrl = null, string? sessionNotes = null)
    {
        var mode = MapMode(modeDisplay);
        var req = new UpdateLessonRequest(
            title, description, subject, startTime, endTime,
            mode, location, meetingUrl, sessionNotes);
        return await _api.PutWithErrorAsync<LessonDto>($"api/lessons/{id}", req);
    }

    public async Task<bool> DeleteLessonAsync(Guid id) =>
        await _api.DeleteAsync($"api/lessons/{id}");

    public async Task<LessonDto?> CancelLessonAsync(Guid id, string? reason = null) =>
        await _api.PostAsync<LessonDto>($"api/lessons/{id}/cancel", new { reason });

    public async Task<LessonDto?> MarkTutorNoShowAsync(Guid id, string? notes = null) =>
        await _api.PostAsync<LessonDto>($"api/lessons/{id}/tutor-no-show", new { notes });

    public async Task<LessonDto?> ResolveLiabilityAsync(Guid id, string resolution) =>
        await _api.PostAsync<LessonDto>($"api/lessons/{id}/resolve-liability", new { resolution });

    public async Task<List<LessonAttendanceDto>> GetAttendancesAsync(Guid lessonId) =>
        await _api.GetAsync<List<LessonAttendanceDto>>($"api/lessons/{lessonId}/attendances") ?? [];

    public async Task<LessonAttendanceDto?> SetAttendanceAsync(Guid lessonId, Guid studentId, bool isPresent, string? notes = null) =>
        await _api.PutAsync<LessonAttendanceDto>($"api/lessons/{lessonId}/attendances",
            new { studentId, isPresent, notes });

    public static string ModeToDisplay(string apiMode) => apiMode switch
    {
        "Online"   => "En ligne",
        "InPerson" => "Présentiel",
        "Hybrid"   => "Hybride",
        _          => apiMode
    };

    public static string DeriveStatus(DateTime startTime, DateTime endTime, string? settlementStatus = null)
    {
        if (!string.IsNullOrEmpty(settlementStatus))
        {
            return settlementStatus switch
            {
                "CancelledFree" => "Annulé",
                "Validated" => "Validé",
                "TutorNoShow" => "Moniteur absent",
                "LiabilityResolved" => "Imputation réglée",
                _ => DeriveTimeStatus(startTime, endTime)
            };
        }

        return DeriveTimeStatus(startTime, endTime);
    }

    private static string DeriveTimeStatus(DateTime startTime, DateTime endTime)
    {
        var now = DateTime.UtcNow;
        if (startTime > now) return "Planifié";
        if (endTime < now) return "Terminé";
        return "En cours";
    }

    private static LessonMode MapMode(string display) => display switch
    {
        "Présentiel" => LessonMode.InPerson,
        "Hybride"    => LessonMode.Hybrid,
        _            => LessonMode.Online
    };
}
