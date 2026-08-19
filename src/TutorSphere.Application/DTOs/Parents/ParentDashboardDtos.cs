namespace TutorSphere.Application.DTOs.Parents;

public record ParentDashboardDto(
    ParentDto Parent,
    decimal? AverageGrade,
    int LessonsTodayCount,
    DateTime? NextLessonStartTime,
    ParentDashboardSubscriptionDto? ActiveSubscription,
    IReadOnlyList<ParentDashboardChildDto> Children,
    IReadOnlyList<ParentDashboardSessionDto> UpcomingSessions,
    IReadOnlyList<ParentDashboardHomeworkDto> PendingHomework,
    IReadOnlyList<ParentDashboardReportDto> RecentReports,
    IReadOnlyList<ParentDashboardMessageDto> RecentMessages,
    ParentDashboardPaymentDto? RecentPayment,
    IReadOnlyList<ParentDashboardDocumentDto> RecentDocuments,
    IReadOnlyList<ParentDashboardCalendarDayDto> WeekCalendar,
    int PendingPaymentsCount = 0,
    decimal PendingPaymentsAmount = 0,
    string? PendingPaymentsCurrency = null);

public record ParentDashboardChildDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? PhotoUrl,
    string? SchoolLevel,
    decimal? AverageGrade,
    int? ProgressPercent,
    DateTime? NextLessonStartTime,
    string? NextLessonSubject);

public record ParentDashboardSessionDto(
    Guid LessonId,
    string TutorName,
    string? Subject,
    DateTime StartTime,
    string Mode);

public record ParentDashboardHomeworkDto(
    Guid Id,
    string Title,
    string ChildName,
    DateTime? DueDate,
    bool IsSubmitted,
    bool IsGraded);

public record ParentDashboardReportDto(
    Guid Id,
    string TutorName,
    string? Subject,
    string? TopicsStudied,
    DateTime CreatedAt,
    string StudentName);

public record ParentDashboardMessageDto(
    Guid Id,
    string From,
    string Preview,
    bool IsUnread,
    DateTime CreatedAt);

public record ParentDashboardPaymentDto(
    Guid Id,
    decimal Amount,
    string Currency,
    string Status,
    DateTime? CompletedAt);

public record ParentDashboardDocumentDto(
    Guid Id,
    string Name,
    long FileSizeBytes,
    string ContentType,
    string? FileUrl,
    DateTime CreatedAt);

public record ParentDashboardSubscriptionDto(
    Guid Id,
    string PlanName,
    string Status,
    DateTime EndDate);

public record ParentDashboardCalendarDayDto(
    DateTime Date,
    string Label,
    bool IsToday,
    IReadOnlyList<ParentDashboardCalendarEventDto> Events);

public record ParentDashboardCalendarEventDto(
    string Title,
    string Subtitle,
    string Time,
    string Color);

/// <summary>
/// Suivi parental d'un enfant. Jamais de date de naissance, courriel, téléphone ni code d'accès.
/// </summary>
public record ParentChildFollowUpDto(
    Guid StudentId,
    int? ProgressPercent,
    int? ProgressDeltaThisMonth,
    int HomeworkDueCount,
    ParentChildNextLessonDto? NextLesson,
    int? AttendancePercentThisMonth,
    IReadOnlyList<ParentChildSubjectProgressDto> SubjectProgress,
    IReadOnlyList<ParentChildHomeworkItemDto> RecentHomework,
    IReadOnlyList<ParentChildTeacherDto> Teachers,
    IReadOnlyList<ParentDashboardDocumentDto> Documents,
    bool HasLoginAccess);

public record ParentChildNextLessonDto(
    Guid LessonId,
    string Subject,
    string? Title,
    DateTime StartTime,
    DateTime EndTime,
    string TeacherName);

public record ParentChildSubjectProgressDto(
    string Subject,
    int? Percent);

public record ParentChildHomeworkItemDto(
    Guid Id,
    string Title,
    string? Subject,
    DateTime? DueDate,
    bool IsSubmitted,
    bool IsGraded,
    decimal? Grade,
    DateTime CreatedAt);

public record ParentChildTeacherDto(
    string UserId,
    string DisplayName,
    string? Subject);

/// <summary>Planning familial. Aucun courriel, téléphone, adresse ni code d'accès.</summary>
public record ParentCalendarDto(
    IReadOnlyList<ParentCalendarChildDto> Children,
    IReadOnlyList<ParentCalendarEventDto> Events);

public record ParentCalendarChildDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? SchoolLevel);

public record ParentCalendarEventDto(
    Guid Id,
    string Kind,
    Guid StudentId,
    string ChildFirstName,
    string Subject,
    DateTime StartTime,
    DateTime EndTime,
    string TeacherName,
    string? TeacherUserId,
    string? Status,
    string? MeetingUrl,
    string? Title,
    bool CanJoinLive = false,
    bool PaymentRequired = false);

public record ParentHomeworkBoardDto(
    IReadOnlyList<ParentHomeworkChildDto> Children,
    IReadOnlyList<ParentHomeworkItemDto> Items,
    IReadOnlyList<ParentHomeworkResultDto> RecentResults);

public record ParentHomeworkChildDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? PhotoUrl,
    string? SchoolLevel,
    int? OnTimePercent);

public record ParentHomeworkItemDto(
    Guid Id,
    Guid StudentId,
    string ChildFirstName,
    string Title,
    string? Subject,
    string TeacherName,
    string? TeacherUserId,
    DateTime? DueDate,
    string Status,
    int AttachmentCount,
    bool CanRemind);

public record ParentHomeworkResultDto(
    Guid Id,
    Guid StudentId,
    string ChildFirstName,
    string Title,
    decimal Grade,
    string? Feedback,
    DateTime GradedAt);

public record ParentHomeworkDetailDto(
    Guid Id,
    Guid StudentId,
    string ChildFirstName,
    string Title,
    string? Subject,
    string? Description,
    string? Instructions,
    IReadOnlyList<ParentHomeworkBlockDto> Content,
    DateTime? DueDate,
    string Status,
    string TeacherName,
    string? TeacherUserId,
    DateTime? SubmittedAt,
    string? SubmissionText,
    IReadOnlyList<ParentHomeworkFileDto> SubmissionFiles,
    decimal? Grade,
    string? Feedback,
    bool IsGraded,
    bool CanRemind);

public record ParentHomeworkBlockDto(
    string Type,
    string? Title,
    string? Body,
    string? Url);

public record ParentHomeworkFileDto(
    string Name,
    string? Url);

/// <summary>Progression d'un seul enfant. Pas de comparaison nominative entre frères et sœurs.</summary>
public record ParentProgressDto(
    IReadOnlyList<ParentProgressChildDto> Children,
    ParentProgressReportDto? Report);

public record ParentProgressChildDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? SchoolLevel,
    string? PhotoUrl);

public record ParentProgressReportDto(
    Guid StudentId,
    string FirstName,
    string LastName,
    string? SchoolLevel,
    int? ProgressPercent,
    int? ProgressDelta,
    decimal? AverageGrade,
    decimal? AverageDelta,
    int? AttendancePercent,
    int? AttendanceDelta,
    int SkillsAcquired,
    int SkillsTotal,
    int SkillsAcquiredDelta,
    bool HasGroupBenchmark,
    IReadOnlyList<ParentProgressPointDto> Timeline,
    IReadOnlyList<ParentProgressSubjectDto> Subjects,
    IReadOnlyList<ParentProgressSkillDto> Skills,
    IReadOnlyList<ParentProgressObservationDto> Observations,
    IReadOnlyList<ParentProgressAttentionDto> Attention,
    int GoalsAchieved,
    int GoalsTotal,
    IReadOnlyList<ParentProgressGoalDto> Goals);

public record ParentProgressPointDto(
    DateTime MonthStart,
    int Percent,
    int? GroupAveragePercent);

public record ParentProgressSubjectDto(
    string Subject,
    int? Percent,
    string Band);

public record ParentProgressSkillDto(
    string Name,
    string? Subject,
    string Status,
    int? Percent);

public record ParentProgressObservationDto(
    Guid Id,
    DateTime CreatedAt,
    string TeacherName,
    string? TeacherUserId,
    string? Subject,
    string Text);

public record ParentProgressAttentionDto(
    string Title,
    string Recommendation);

public record ParentProgressGoalDto(
    string Title,
    bool Achieved);
