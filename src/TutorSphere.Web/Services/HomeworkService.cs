using TutorSphere.Application.DTOs.Homework;

namespace TutorSphere.Web.Services;

public sealed class HomeworkService
{
    private readonly ApiClient _api;

    public HomeworkService(ApiClient api) => _api = api;

    public async Task<List<HomeworkDto>> GetAllAsync() =>
        await _api.GetAsync<List<HomeworkDto>>("api/homework") ?? [];

    public async Task<List<HomeworkDto>> GetHomeworkByStudentAsync(Guid studentId) =>
        await _api.GetAsync<List<HomeworkDto>>($"api/homework/student/{studentId}") ?? [];

    public async Task<HomeworkDto?> GetByIdAsync(Guid id) =>
        await _api.GetAsync<HomeworkDto>($"api/homework/{id}");

    public async Task<HomeworkDto?> CreateHomeworkAsync(CreateHomeworkRequest req) =>
        await _api.PostAsync<HomeworkDto>("api/homework", req);

    public async Task<List<HomeworkDto>> CreateBatchAsync(CreateHomeworkBatchRequest req) =>
        await _api.PostAsync<List<HomeworkDto>>("api/homework/batch", req) ?? [];

    public async Task<HomeworkDto?> UpdateHomeworkAsync(Guid id, UpdateHomeworkRequest req) =>
        await _api.PutAsync<HomeworkDto>($"api/homework/{id}", req);

    public async Task<HomeworkDto?> GradeHomeworkAsync(Guid id, GradeHomeworkRequest req) =>
        await _api.PostAsync<HomeworkDto>($"api/homework/{id}/grade", req);

    public Task DeleteHomeworkAsync(Guid id) =>
        _api.DeleteAsync($"api/homework/{id}");
}
