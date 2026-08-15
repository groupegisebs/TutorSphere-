namespace TutorSphere.Application.Common;

/// <summary>Textes dynamiques injectés dans les e-mails (hors templates Mail Sender).</summary>
public static class EmailCopy
{
    public static string EnrollmentAcceptedNote(string language, bool needsPayment) =>
        SupportedLanguageCodes.Normalize(language) switch
        {
            SupportedLanguageCodes.English => needsPayment
                ? "Your enrollment is accepted. Please complete payment to activate the lessons."
                : "Your enrollment is accepted and active. Lessons will be scheduled soon.",
            SupportedLanguageCodes.Spanish => needsPayment
                ? "Su inscripción ha sido aceptada. Complete el pago para activar las clases."
                : "Su inscripción ha sido aceptada y está activa. Las clases se programarán pronto.",
            SupportedLanguageCodes.German => needsPayment
                ? "Ihre Anmeldung wurde angenommen. Bitte zahlen Sie, um den Unterricht zu aktivieren."
                : "Ihre Anmeldung wurde angenommen und ist aktiv. Der Unterricht wird in Kürze geplant.",
            SupportedLanguageCodes.Portuguese => needsPayment
                ? "A sua inscrição foi aceite. Conclua o pagamento para ativar as aulas."
                : "A sua inscrição foi aceite e está ativa. As aulas serão agendadas em breve.",
            SupportedLanguageCodes.MandarinChinese => needsPayment
                ? "您的报名已接受。请完成付款以激活课程。"
                : "您的报名已接受并已激活。课程将很快安排。",
            SupportedLanguageCodes.Arabic => needsPayment
                ? "تم قبول تسجيلك. يرجى إتمام الدفع لتفعيل الحصص."
                : "تم قبول تسجيلك وهو نشط. ستُجدول الحصص قريبًا.",
            _ => needsPayment
                ? "Votre inscription est acceptée. Veuillez procéder au paiement pour activer les cours."
                : "Votre inscription est acceptée et active. Les cours seront planifiés prochainement."
        };

    public static string EnrollmentRejectedNote(string language) =>
        SupportedLanguageCodes.Normalize(language) switch
        {
            SupportedLanguageCodes.English => "The teacher declined this enrollment. You can choose another offer.",
            SupportedLanguageCodes.Spanish => "El docente rechazó esta inscripción. Puede elegir otra oferta.",
            SupportedLanguageCodes.German => "Der Lehrer hat diese Anmeldung abgelehnt. Sie können ein anderes Angebot wählen.",
            SupportedLanguageCodes.Portuguese => "O professor recusou esta inscrição. Pode escolher outra oferta.",
            SupportedLanguageCodes.MandarinChinese => "教师已拒绝此次报名。您可以选择其他课程。",
            SupportedLanguageCodes.Arabic => "رفض المعلم هذا التسجيل. يمكنك اختيار عرض آخر.",
            _ => "L'enseignant a refusé cette inscription. Vous pouvez choisir une autre offre."
        };

    public static string UnspecifiedReason(string language) =>
        SupportedLanguageCodes.Normalize(language) switch
        {
            SupportedLanguageCodes.English => "Not specified",
            SupportedLanguageCodes.Spanish => "No especificado",
            SupportedLanguageCodes.German => "Nicht angegeben",
            SupportedLanguageCodes.Portuguese => "Não especificado",
            SupportedLanguageCodes.MandarinChinese => "未指定",
            SupportedLanguageCodes.Arabic => "غير محدد",
            _ => "Non spécifié"
        };
}
