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
        var mode = MapMode(modeDisplay);
        var req = new UpdateLessonRequest(
            title, description, subject, startTime, endTime,
            mode, location, meetingUrl, sessionNotes);
        return await _api.PutAsync<LessonDto>($"api/lessons/{id}", req);
    }

    public async Task<bool> DeleteLessonAsync(Guid id) =>
        await _api.DeleteAsync($"api/lessons/{id}");

    public static string ModeToDisplay(string apiMode) => apiMode switch
    {
        "Online"   => "En ligne",
        "InPerson" => "Présentiel",
        "Hybrid"   => "Hybride",
        _          => apiMode
    };

    public static string DeriveStatus(DateTime startTime, DateTime endTime)
    {
        var now = DateTime.UtcNow;
        if (startTime > now) return "Planifié";
        if (endTime < now)   return "Terminé";
        return "En cours";
    }

    private static LessonMode MapMode(string display) => display switch
    {
        "Présentiel" => LessonMode.InPerson,
        "Hybride"    => LessonMode.Hybrid,
        _            => LessonMode.Online
    };
}
