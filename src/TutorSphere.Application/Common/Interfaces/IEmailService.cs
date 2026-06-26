namespace TutorSphere.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendWelcomeAsync(string email, string firstName, CancellationToken ct = default);

    Task SendLessonReportToParentAsync(
        string parentEmail,
        string parentFirstName,
        string studentName,
        string tutorName,
        CancellationToken ct = default);

    Task SendSchoolCreatedAsync(
        string ownerEmail,
        string ownerFirstName,
        string schoolName,
        CancellationToken ct = default);
}
