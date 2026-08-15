using TutorSphere.Application.Services;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.UnitTests;

public class LessonAccessServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _lessonId = Guid.NewGuid();
    private readonly Guid _subId = Guid.NewGuid();

    private (LessonAccessService Access, MemoryAppDb Db) Create(bool enrolled = true)
    {
        var db = new MemoryAppDb();
        db.StudentsList.Add(new Student
        {
            Id = _studentId,
            TenantId = _tenantId,
            FirstName = "Lina",
            LastName = "Ngo",
            UserId = "student-user",
            DateOfBirth = DateTime.Today.AddYears(-10),
            IsActive = true
        });
        db.LessonsList.Add(new Lesson
        {
            Id = _lessonId,
            TenantId = _tenantId,
            Title = "Maths",
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(2),
            SessionNotes = SubscriptionLessonScheduler.MarkerFor(_subId)
        });
        if (enrolled)
        {
            db.AttendancesList.Add(new LessonAttendance
            {
                LessonId = _lessonId,
                StudentId = _studentId,
                TenantId = _tenantId
            });
        }

        return (new LessonAccessService(db, new StubUrls()), db);
    }

    private void AddPack(MemoryAppDb db, SubscriptionStatus status, int remaining, DateTime? end = null)
    {
        db.SubscriptionsList.Add(new StudentSubscription
        {
            Id = _subId,
            TenantId = _tenantId,
            StudentId = _studentId,
            OfferingId = Guid.NewGuid(),
            Status = status,
            SessionsRemaining = remaining,
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = end ?? DateTime.UtcNow.AddDays(20)
        });
    }

    [Fact]
    public void Unpaid_student_cannot_join()
    {
        var (access, db) = Create();
        AddPack(db, SubscriptionStatus.AwaitingPayment, remaining: 0);

        Assert.False(access.CanAttendLesson(_studentId, _lessonId));
        var dto = access.Evaluate(_studentId, _lessonId);
        Assert.False(dto.CanJoin);
        Assert.True(dto.PaymentRequired);
        Assert.Contains("/parent/subscriptions", dto.PayUrl);
    }

    [Fact]
    public void Autonomous_student_is_sent_to_student_checkout()
    {
        var (access, db) = Create();
        db.StudentsList[0].DateOfBirth = DateTime.Today.AddYears(-15);
        AddPack(db, SubscriptionStatus.AwaitingPayment, remaining: 0);

        var dto = access.EvaluateForUser("student-user", _lessonId);
        Assert.True(dto.PaymentRequired);
        Assert.EndsWith("/student/subscriptions", dto.PayUrl);
    }

    [Fact]
    public void Active_pack_with_remaining_sessions_can_join()
    {
        var (access, db) = Create();
        AddPack(db, SubscriptionStatus.Active, remaining: 3);

        Assert.True(access.CanAttendLesson(_studentId, _lessonId));
        Assert.True(access.Evaluate(_studentId, _lessonId).CanJoin);
    }

    [Fact]
    public void Zero_remaining_can_still_join_scheduled_pack_lesson()
    {
        var (access, db) = Create();
        AddPack(db, SubscriptionStatus.Active, remaining: 0);

        Assert.True(access.CanAttendLesson(_studentId, _lessonId));
    }

    [Fact]
    public void Expired_end_date_blocks_even_if_sessions_remain()
    {
        var (access, db) = Create();
        AddPack(db, SubscriptionStatus.Active, remaining: 4, end: DateTime.UtcNow.AddDays(-1));

        Assert.False(access.CanAttendLesson(_studentId, _lessonId));
    }

    [Fact]
    public void Cancelled_lesson_is_never_joinable()
    {
        var (access, db) = Create();
        AddPack(db, SubscriptionStatus.Active, remaining: 4);
        db.LessonsList[0].SettlementStatus = LessonSettlementStatus.CancelledFree;

        Assert.False(access.CanAttendLesson(_studentId, _lessonId));
    }

    [Fact]
    public void Already_counted_session_remains_accessible()
    {
        var (access, db) = Create();
        db.LessonsList[0].SessionCounted = true;

        Assert.True(access.CanAttendLesson(_studentId, _lessonId));
    }

    [Fact]
    public void Stranger_without_attendance_cannot_join()
    {
        var (access, _) = Create(enrolled: false);
        Assert.False(access.CanAttendLesson(_studentId, _lessonId));
    }

    [Fact]
    public void Tutor_cannot_manually_add_unpaid_student()
    {
        var (access, db) = Create();
        AddPack(db, SubscriptionStatus.AwaitingPayment, remaining: 0);

        var ex = Assert.Throws<InvalidOperationException>(
            () => access.EnsureStudentEligibleForManualLesson(_studentId, _tenantId));
        Assert.Contains("forfait actif", ex.Message);
    }

    [Fact]
    public void Tutor_can_add_student_with_usable_pack()
    {
        var (access, db) = Create();
        AddPack(db, SubscriptionStatus.Active, remaining: 1);
        access.EnsureStudentEligibleForManualLesson(_studentId, _tenantId);
    }
}

public class SubscriptionLessonSchedulerTests
{
    [Fact]
    public void Period_marker_changes_on_renewal_so_new_lessons_can_be_created()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var first = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var next = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        var a = SubscriptionLessonScheduler.MarkerForPeriod(id, first);
        var b = SubscriptionLessonScheduler.MarkerForPeriod(id, next);

        Assert.NotEqual(a, b);
        Assert.StartsWith(SubscriptionLessonScheduler.MarkerFor(id), a);
        Assert.Contains(":p20260801", a);
        Assert.Contains(":p20260901", b);
    }

    [Fact]
    public async Task Cancel_unconsumed_future_leaves_past_and_counted_lessons()
    {
        var db = new MemoryAppDb();
        var subId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var marker = SubscriptionLessonScheduler.MarkerFor(subId);

        var future = new Lesson
        {
            TenantId = tenantId,
            Title = "futur",
            StartTime = now.AddDays(3),
            EndTime = now.AddDays(3).AddHours(1),
            SessionNotes = marker,
            SessionCounted = false
        };
        var past = new Lesson
        {
            TenantId = tenantId,
            Title = "passé",
            StartTime = now.AddDays(-2),
            EndTime = now.AddDays(-2).AddHours(1),
            SessionNotes = marker
        };
        var counted = new Lesson
        {
            TenantId = tenantId,
            Title = "compté",
            StartTime = now.AddDays(4),
            EndTime = now.AddDays(4).AddHours(1),
            SessionNotes = marker,
            SessionCounted = true
        };
        db.LessonsList.AddRange([future, past, counted]);
        db.SubscriptionsList.Add(new StudentSubscription
        {
            Id = subId,
            TenantId = tenantId,
            StudentId = Guid.NewGuid(),
            OfferingId = Guid.NewGuid(),
            Status = SubscriptionStatus.Cancelled,
            StartDate = now.AddDays(-10),
            EndDate = now.AddDays(20)
        });

        var scheduler = new SubscriptionLessonScheduler(
            db,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SubscriptionLessonScheduler>.Instance);

        var n = await scheduler.CancelUnconsumedFutureAsync(subId);

        Assert.Equal(1, n);
        Assert.Equal(LessonSettlementStatus.CancelledFree, future.SettlementStatus);
        Assert.Equal(LessonSettlementStatus.Scheduled, past.SettlementStatus);
        Assert.Equal(LessonSettlementStatus.Scheduled, counted.SettlementStatus);
    }

    [Fact]
    public async Task EnsureScheduled_is_noop_when_not_active()
    {
        var db = new MemoryAppDb();
        var subId = Guid.NewGuid();
        db.SubscriptionsList.Add(new StudentSubscription
        {
            Id = subId,
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            OfferingId = Guid.NewGuid(),
            Status = SubscriptionStatus.AwaitingPayment,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var scheduler = new SubscriptionLessonScheduler(
            db,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SubscriptionLessonScheduler>.Instance);

        Assert.Equal(0, await scheduler.EnsureScheduledAsync(subId));
        Assert.Empty(db.LessonsList);
    }
}

public class PaymentAccessContractTests
{
    [Fact]
    public void Lesson_and_calendar_dtos_expose_payment_gate_fields()
    {
        var lesson = typeof(TutorSphere.Application.DTOs.Lessons.LessonDto).GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
        var calendar = typeof(TutorSphere.Application.DTOs.Parents.ParentCalendarEventDto).GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CanJoin", lesson);
        Assert.Contains("PaymentRequired", lesson);
        Assert.Contains("CanJoinLive", calendar);
        Assert.Contains("PaymentRequired", calendar);
    }
}
