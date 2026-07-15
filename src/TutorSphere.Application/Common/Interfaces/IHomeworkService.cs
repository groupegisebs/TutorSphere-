using TutorSphere.Application.DTOs.Homework;

namespace TutorSphere.Application.Common.Interfaces;

public interface IHomeworkService
{
    Task<HomeworkDto> CreateAsync(CreateHomeworkRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<HomeworkDto>> CreateBatchAsync(CreateHomeworkBatchRequest request, CancellationToken ct = default);
    Task<HomeworkDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<HomeworkDto>> GetByStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<IReadOnlyList<HomeworkDto>> GetForCurrentTenantAsync(CancellationToken ct = default);
    Task<HomeworkDto> UpdateAsync(Guid id, UpdateHomeworkRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<HomeworkDto> SubmitAsync(Guid id, SubmitHomeworkRequest request, CancellationToken ct = default);
    Task<HomeworkDto> GradeAsync(Guid id, GradeHomeworkRequest request, CancellationToken ct = default);
}
