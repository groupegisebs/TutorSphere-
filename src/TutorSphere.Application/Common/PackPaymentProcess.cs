using TutorSphere.Domain.Entities;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Common;

/// <summary>
/// Processus forfait : inscription → acceptation → paiement unique → accès cours.
/// Pas de portefeuille. Toutes les décisions d'argent et d'activation passent ici.
/// </summary>
public static class PackPaymentProcess
{
    /// <summary>À l'inscription, aucune séance n'est créditée tant que le pack n'est pas payé (ou gratuit).</summary>
    public const int EnrollmentSessionsRemaining = 0;

    public enum Decision
    {
        AlreadyApplied,
        Unchanged,
        RecordFailure,
        RefundClosedSubscription,
        ActivatePack,
        CompleteWithoutSubscription
    }

    public static PaymentStatus MapGatewayStatus(string? gatewayStatus) =>
        (gatewayStatus ?? "").ToUpperInvariant() switch
        {
            "SUCCEEDED" => PaymentStatus.Completed,
            "FAILED" or "CANCELLED" or "CANCELED" or "EXPIRED" => PaymentStatus.Failed,
            "REFUNDED" or "PARTIALLYREFUNDED" => PaymentStatus.Refunded,
            _ => PaymentStatus.Pending
        };

    public static Decision Decide(
        PaymentStatus previous,
        PaymentStatus mapped,
        SubscriptionStatus? subscriptionStatus)
    {
        if (previous == PaymentStatus.Completed)
            return Decision.AlreadyApplied;

        if (mapped == previous && mapped != PaymentStatus.Completed)
            return Decision.Unchanged;

        if (mapped == PaymentStatus.Failed)
            return Decision.RecordFailure;

        if (mapped != PaymentStatus.Completed)
            return Decision.Unchanged;

        if (subscriptionStatus is SubscriptionStatus.Rejected or SubscriptionStatus.Cancelled)
            return Decision.RefundClosedSubscription;

        if (subscriptionStatus is null)
            return Decision.CompleteWithoutSubscription;

        return Decision.ActivatePack;
    }

    public static bool IsFirstActivation(SubscriptionStatus status) =>
        status is SubscriptionStatus.AwaitingPayment
            or SubscriptionStatus.Pending
            or SubscriptionStatus.Expired;

    public static int SessionsOnFreeAccept(int offeringSessionCount) => Math.Max(0, offeringSessionCount);

    /// <summary>
    /// Crédite le forfait une seule fois par paiement : SET au premier encaissement, ADD au renouvellement.
    /// </summary>
    public static void ActivatePack(
        StudentSubscription subscription,
        int sessionCount,
        int durationDays,
        DateTime utcNow)
    {
        var credits = Math.Max(0, sessionCount);
        var days = durationDays > 0 ? durationDays : 30;
        var first = IsFirstActivation(subscription.Status);
        var periodStart = utcNow;
        if (!first
            && subscription.Status == SubscriptionStatus.Active
            && subscription.EndDate > utcNow)
        {
            periodStart = subscription.EndDate;
        }

        subscription.Status = SubscriptionStatus.Active;
        subscription.StartDate = periodStart;
        subscription.EndDate = periodStart.AddDays(days);
        subscription.RenewalReminderSentAt = null;
        subscription.LowSessionsReminderSentAt = null;
        subscription.LessonAccessReminderSentAt = null;
        subscription.UpdatedAt = utcNow;
        if (first)
            subscription.SessionsRemaining = credits;
        else
            subscription.SessionsRemaining += credits;
    }

    public static void EnsurePayable(StudentSubscription subscription, int durationDays, DateTime utcNow)
    {
        if (subscription.Status == SubscriptionStatus.AwaitingPayment)
            return;

        if (subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Paused)
        {
            var windowDays = SubscriptionPackRules.RenewalWindowDays(durationDays > 0 ? durationDays : 30);
            if (utcNow >= subscription.EndDate.AddDays(-windowDays))
                return;

            throw new InvalidOperationException(
                $"Le renouvellement sera disponible {windowDays} jour(s) avant la fin du forfait.");
        }

        throw new InvalidOperationException(
            "Le paiement n'est possible qu'après acceptation de la demande par l'enseignant, ou pour un renouvellement.");
    }

    public static bool CanCallerPay(
        string userId,
        string? studentUserId,
        string? parentUserId,
        string? tutorOwnerUserId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return string.Equals(studentUserId, userId, StringComparison.Ordinal)
               || string.Equals(parentUserId, userId, StringComparison.Ordinal)
               || string.Equals(tutorOwnerUserId, userId, StringComparison.Ordinal);
    }

    public static void ClosePendingPayment(Payment payment, DateTime utcNow)
    {
        if (payment.Status != PaymentStatus.Pending)
            return;

        payment.Status = PaymentStatus.Failed;
        payment.UpdatedAt = utcNow;
    }
}
