using TutorSphere.Application.DTOs.Homework;
using TutorSphere.Web.Services;

namespace TutorSphere.Web.Services;

public sealed class HomeworkService
{
    private readonly ApiClient _api;

    public HomeworkService(ApiClient api) => _api = api;

    public async Task<List<HomeworkDto>> GetAllAsync() =>
        await _api.GetAsync<List<HomeworkDto>>("api/homework") ?? [];

    public async Task<ApiResult<List<HomeworkDto>>> GetAllWithErrorAsync() =>
        await _api.GetWithErrorAsync<List<HomeworkDto>>("api/homework");

    public async Task<List<HomeworkDto>> GetHomeworkByStudentAsync(Guid studentId) =>
        await _api.GetAsync<List<HomeworkDto>>($"api/homework/student/{studentId}") ?? [];

    public async Task<HomeworkDto?> GetByIdAsync(Guid id) =>
        await _api.GetAsync<HomeworkDto>($"api/homework/{id}");

    public async Task<ApiResult<HomeworkDto>> CreateHomeworkAsync(CreateHomeworkRequest req) =>
        await _api.PostWithErrorAsync<HomeworkDto>("api/homework", req);

    public async Task<ApiResult<List<HomeworkDto>>> CreateBatchAsync(CreateHomeworkBatchRequest req) =>
        await _api.PostWithErrorAsync<List<HomeworkDto>>("api/homework/batch", req);

    public async Task<ApiResult<HomeworkDto>> UpdateHomeworkAsync(Guid id, UpdateHomeworkRequest req) =>
        await _api.PutWithErrorAsync<HomeworkDto>($"api/homework/{id}", req);

    public async Task<ApiResult<HomeworkDto>> GradeHomeworkAsync(Guid id, GradeHomeworkRequest req) =>
        await _api.PostWithErrorAsync<HomeworkDto>($"api/homework/{id}/grade", req);

    public async Task<ApiResult<bool>> DeleteHomeworkAsync(Guid id) =>
        await _api.DeleteWithErrorAsync($"api/homework/{id}");
}
