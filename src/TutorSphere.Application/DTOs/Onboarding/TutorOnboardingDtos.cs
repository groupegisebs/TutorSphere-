namespace TutorSphere.Application.DTOs.Onboarding;

public record TutorOnboardingStatusDto(
    Guid TenantId,
    string SchoolName,
    bool HasPaidLicense,
    bool RequiresOnboarding,
    bool IsFullyActive,
    DateTime? OnboardingCompletedAt,
    DateTime? LicenseExpiresAt,
    IReadOnlyList<TutorOnboardingModuleDto> Modules,
    int CompletedModuleCount,
    int TotalModuleCount);

public record TutorOnboardingModuleDto(
    string Id,
    int Order,
    string Title,
    string Summary,
    string BodyHtml,
    IReadOnlyList<TutorOnboardingQuizItemDto> Quiz,
    bool IsCompleted);

public record TutorOnboardingQuizItemDto(
    string Question,
    IReadOnlyList<string> Choices,
    int CorrectIndex);

public record CompleteOnboardingModuleRequest(string ModuleId, IReadOnlyList<int> QuizAnswers);

public record CompleteOnboardingModuleResult(
    string ModuleId,
    bool ModuleCompleted,
    bool OnboardingCompleted,
    bool IsFullyActive,
    string? Error);
