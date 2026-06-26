using TutorSphere.Application.DTOs.LessonReports;

namespace TutorSphere.Web.Services;

public sealed class ReportService
{
    private readonly ApiClient _api;

    public ReportService(ApiClient api) => _api = api;

    public async Task<List<LessonReportDto>> GetReportsByLessonAsync(Guid lessonId) =>
        await _api.GetAsync<List<LessonReportDto>>($"api/lessonreports/lesson/{lessonId}") ?? [];

    public async Task<List<LessonReportDto>> GetReportsByStudentAsync(Guid studentId) =>
        await _api.GetAsync<List<LessonReportDto>>($"api/lessonreports/student/{studentId}") ?? [];

    public async Task<LessonReportDto?> CreateReportAsync(CreateLessonReportRequest req) =>
        await _api.PostAsync<LessonReportDto>("api/lessonreports", req);

    public async Task<LessonReportDto?> UpdateReportAsync(Guid id, UpdateLessonReportRequest req) =>
        await _api.PutAsync<LessonReportDto>($"api/lessonreports/{id}", req);

    public async Task<LessonReportDto?> SendToParentAsync(Guid id) =>
        await _api.PostAsync<LessonReportDto>($"api/lessonreports/{id}/send-to-parent", new { });

    public async Task<bool> DeleteReportAsync(Guid id) =>
        await _api.DeleteAsync($"api/lessonreports/{id}");
}
