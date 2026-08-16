namespace TutorSphere.Application.DTOs.Documents;

public record DocumentDto(
    Guid Id,
    string FileName,
    long FileSize,
    string ContentType,
    DateTime UploadedAt,
    Guid? StudentId,
    Guid? LessonId,
    string Url,
    string? Folder = null,
    string? Title = null,
    string? Subject = null,
    string? SchoolLevel = null,
    string? Summary = null,
    IReadOnlyList<Guid>? SharedStudentIds = null,
    Guid? SharedByExpertGroupId = null,
    Guid? LibraryBatchId = null);

public record UploadDocumentRequest(
    Guid? StudentId,
    Guid? LessonId,
    string? Folder);

public record DocumentWriteRequest(
    string? Title = null,
    string? Subject = null,
    string? SchoolLevel = null,
    string? Summary = null,
    IReadOnlyList<Guid>? SharedStudentIds = null,
    Guid? SharedByExpertGroupId = null,
    Guid? LibraryBatchId = null,
    Guid? TenantId = null);

public record ExpertLibraryDocumentDto(
    Guid BatchId,
    Guid DocumentId,
    string Title,
    string FileName,
    string? Subject,
    string? SchoolLevel,
    string? Summary,
    string? Folder,
    DateTime UploadedAt,
    IReadOnlyList<string> SharedTeachers);
