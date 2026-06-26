namespace TutorSphere.Application.DTOs.Documents;

public record DocumentDto(
    Guid Id,
    string FileName,
    long FileSize,
    string ContentType,
    DateTime UploadedAt,
    Guid? StudentId,
    Guid? LessonId,
    string Url);

public record UploadDocumentRequest(
    Guid? StudentId,
    Guid? LessonId,
    string? Folder);
