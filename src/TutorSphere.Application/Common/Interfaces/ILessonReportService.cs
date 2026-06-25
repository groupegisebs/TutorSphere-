using TutorSphere.Application.DTOs.LessonReports;

namespace TutorSphere.Application.Common.Interfaces;

public interface ILessonReportService
{
    Task<LessonReportDto> CreateAsync(CreateLessonReportRequest request, CancellationToken ct = default);
    Task<LessonReportDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LessonReportDto>> GetByLessonAsync(Guid lessonId, CancellationToken ct = default);
    Task<IReadOnlyList<LessonReportDto>> GetByStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<LessonReportDto> UpdateAsync(Guid id, UpdateLessonReportRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<LessonReportDto> MarkSentToParentAsync(Guid id, CancellationToken ct = default);
}
