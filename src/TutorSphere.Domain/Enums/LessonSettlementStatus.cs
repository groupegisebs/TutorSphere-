namespace TutorSphere.Domain.Enums;

/// <summary>
/// Règles métier :
/// - Annulation ≥ 24 h avant le début → CancelledFree (non comptée).
/// - Non annulée à temps / élève absent → Validated (comptée).
/// - Moniteur absent → TutorNoShow (non comptée, moniteur imputable : replanifier ou rembourser).
/// </summary>
public enum LessonSettlementStatus
{
    Scheduled = 0,
    CancelledFree = 1,
    Validated = 2,
    TutorNoShow = 3,
    LiabilityResolved = 4
}
