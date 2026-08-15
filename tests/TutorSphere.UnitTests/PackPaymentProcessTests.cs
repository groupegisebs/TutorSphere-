using TutorSphere.Application.Common;
using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.UnitTests;

public class PackPaymentProcessTests
{
    private static StudentSubscription NewSub(SubscriptionStatus status, int remaining = 0)
    {
        var now = DateTime.UtcNow;
        return new StudentSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            OfferingId = Guid.NewGuid(),
            Status = status,
            StartDate = now,
            EndDate = now.AddDays(30),
            SessionsRemaining = remaining
        };
    }

    [Theory]
    [InlineData("Succeeded", PaymentStatus.Completed)]
    [InlineData("SUCCEEDED", PaymentStatus.Completed)]
    [InlineData("Failed", PaymentStatus.Failed)]
    [InlineData("CANCELLED", PaymentStatus.Failed)]
    [InlineData("CANCELED", PaymentStatus.Failed)]
    [InlineData("EXPIRED", PaymentStatus.Failed)]
    [InlineData("Refunded", PaymentStatus.Refunded)]
    [InlineData("PartiallyRefunded", PaymentStatus.Refunded)]
    [InlineData("Processing", PaymentStatus.Pending)]
    [InlineData("RequiresReview", PaymentStatus.Pending)]
    [InlineData(null, PaymentStatus.Pending)]
    public void MapGatewayStatus_covers_all_gateway_outcomes(string? raw, PaymentStatus expected)
    {
        Assert.Equal(expected, PackPaymentProcess.MapGatewayStatus(raw));
    }

    [Fact]
    public void Decide_is_idempotent_once_completed()
    {
        var again = PackPaymentProcess.Decide(
            PaymentStatus.Completed,
            PaymentStatus.Completed,
            SubscriptionStatus.Active);

        Assert.Equal(PackPaymentProcess.Decision.AlreadyApplied, again);
    }

    [Fact]
    public void Decide_does_not_downgrade_a_completed_payment_to_failed()
    {
        var decision = PackPaymentProcess.Decide(
            PaymentStatus.Completed,
            PaymentStatus.Failed,
            SubscriptionStatus.Active);

        Assert.Equal(PackPaymentProcess.Decision.AlreadyApplied, decision);
    }

    [Fact]
    public void Decide_records_card_decline()
    {
        var decision = PackPaymentProcess.Decide(
            PaymentStatus.Pending,
            PaymentStatus.Failed,
            SubscriptionStatus.AwaitingPayment);

        Assert.Equal(PackPaymentProcess.Decision.RecordFailure, decision);
    }

    [Fact]
    public void Decide_refunds_money_taken_after_reject_or_cancel()
    {
        Assert.Equal(
            PackPaymentProcess.Decision.RefundClosedSubscription,
            PackPaymentProcess.Decide(PaymentStatus.Pending, PaymentStatus.Completed, SubscriptionStatus.Rejected));
        Assert.Equal(
            PackPaymentProcess.Decision.RefundClosedSubscription,
            PackPaymentProcess.Decide(PaymentStatus.Pending, PaymentStatus.Completed, SubscriptionStatus.Cancelled));
    }

    [Fact]
    public void Decide_activates_pack_on_first_success()
    {
        var decision = PackPaymentProcess.Decide(
            PaymentStatus.Pending,
            PaymentStatus.Completed,
            SubscriptionStatus.AwaitingPayment);

        Assert.Equal(PackPaymentProcess.Decision.ActivatePack, decision);
    }

    [Fact]
    public void Enrollment_never_credits_sessions()
    {
        Assert.Equal(0, PackPaymentProcess.EnrollmentSessionsRemaining);
    }

    [Fact]
    public void First_payment_sets_sessions_from_zero()
    {
        var sub = NewSub(SubscriptionStatus.AwaitingPayment, remaining: PackPaymentProcess.EnrollmentSessionsRemaining);
        var now = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

        PackPaymentProcess.ActivatePack(sub, sessionCount: 8, durationDays: 30, now);

        Assert.Equal(SubscriptionStatus.Active, sub.Status);
        Assert.Equal(8, sub.SessionsRemaining);
        Assert.Equal(now, sub.StartDate);
        Assert.Equal(now.AddDays(30), sub.EndDate);
    }

    [Fact]
    public void Completed_poll_is_skipped_so_sessions_are_not_added_twice()
    {
        var first = PackPaymentProcess.Decide(
            PaymentStatus.Pending, PaymentStatus.Completed, SubscriptionStatus.AwaitingPayment);
        var retry = PackPaymentProcess.Decide(
            PaymentStatus.Completed, PaymentStatus.Completed, SubscriptionStatus.Active);

        Assert.Equal(PackPaymentProcess.Decision.ActivatePack, first);
        Assert.Equal(PackPaymentProcess.Decision.AlreadyApplied, retry);
    }

    [Fact]
    public void Renewal_adds_sessions_and_extends_from_current_end_date()
    {
        var now = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
        var sub = NewSub(SubscriptionStatus.Active, remaining: 2);
        sub.StartDate = now.AddDays(-20);
        sub.EndDate = now.AddDays(10);

        PackPaymentProcess.ActivatePack(sub, sessionCount: 8, durationDays: 30, now);

        Assert.Equal(10, sub.SessionsRemaining);
        Assert.Equal(now.AddDays(10), sub.StartDate);
        Assert.Equal(now.AddDays(40), sub.EndDate);
        Assert.Null(sub.RenewalReminderSentAt);
        Assert.Null(sub.LowSessionsReminderSentAt);
    }

    [Fact]
    public void Free_accept_credits_offering_sessions()
    {
        Assert.Equal(4, PackPaymentProcess.SessionsOnFreeAccept(4));
        Assert.Equal(0, PackPaymentProcess.SessionsOnFreeAccept(-3));
    }

    [Fact]
    public void Awaiting_payment_is_always_payable()
    {
        var sub = NewSub(SubscriptionStatus.AwaitingPayment);
        PackPaymentProcess.EnsurePayable(sub, 30, DateTime.UtcNow);
    }

    [Fact]
    public void Pending_enrollment_cannot_be_paid_yet()
    {
        var sub = NewSub(SubscriptionStatus.Pending);
        var ex = Assert.Throws<InvalidOperationException>(
            () => PackPaymentProcess.EnsurePayable(sub, 30, DateTime.UtcNow));
        Assert.Contains("acceptation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Weekly_pack_cannot_renew_30_days_early()
    {
        var now = DateTime.UtcNow;
        var sub = NewSub(SubscriptionStatus.Active);
        sub.EndDate = now.AddDays(6);

        var ex = Assert.Throws<InvalidOperationException>(
            () => PackPaymentProcess.EnsurePayable(sub, durationDays: 7, utcNow: now.AddDays(-1)));
        Assert.Contains("2 jour", ex.Message);
    }

    [Fact]
    public void Weekly_pack_can_renew_inside_proportionate_window()
    {
        var now = DateTime.UtcNow;
        var sub = NewSub(SubscriptionStatus.Active);
        sub.EndDate = now.AddHours(12);
        PackPaymentProcess.EnsurePayable(sub, durationDays: 7, utcNow: now);
    }

    [Theory]
    [InlineData("parent-1", "student-1", "parent-1", "tutor-1", true)]
    [InlineData("student-1", "student-1", "parent-1", "tutor-1", true)]
    [InlineData("tutor-1", "student-1", "parent-1", "tutor-1", true)]
    [InlineData("stranger", "student-1", "parent-1", "tutor-1", false)]
    [InlineData("", "student-1", "parent-1", "tutor-1", false)]
    public void Checkout_is_owned_by_parent_student_or_tutor(
        string caller, string? student, string? parent, string? tutor, bool expected)
    {
        Assert.Equal(expected, PackPaymentProcess.CanCallerPay(caller, student, parent, tutor));
    }

    [Fact]
    public void ClosePendingPayment_fails_only_pending_rows()
    {
        var now = DateTime.UtcNow;
        var pending = new Payment { Status = PaymentStatus.Pending };
        var paid = new Payment { Status = PaymentStatus.Completed };

        PackPaymentProcess.ClosePendingPayment(pending, now);
        PackPaymentProcess.ClosePendingPayment(paid, now);

        Assert.Equal(PaymentStatus.Failed, pending.Status);
        Assert.Equal(now, pending.UpdatedAt);
        Assert.Equal(PaymentStatus.Completed, paid.Status);
    }
}

public class SubscriptionPackRulesTests
{
    [Theory]
    [InlineData(7, 2)]
    [InlineData(30, 10)]
    [InlineData(90, 30)]
    [InlineData(365, 30)]
    [InlineData(0, 1)]
    public void Renewal_window_scales_with_duration(int durationDays, int expected)
    {
        Assert.Equal(expected, SubscriptionPackRules.RenewalWindowDays(durationDays));
    }

    [Theory]
    [InlineData(7, "MONTHLY", "Monthly")]
    [InlineData(30, "MONTHLY", "Monthly")]
    [InlineData(90, "ONE-TIME", "OneTime")]
    [InlineData(180, "ONE-TIME", "OneTime")]
    [InlineData(365, "YEARLY", "Yearly")]
    [InlineData(0, "ONE-TIME", "OneTime")]
    public void Catalogue_does_not_sell_quarter_as_yearly(int days, string plan, string interval)
    {
        Assert.Equal(plan, SubscriptionPackRules.ResolvePlanCode(days));
        Assert.Equal(interval, SubscriptionPackRules.ResolveBillingInterval(days));
    }
}
