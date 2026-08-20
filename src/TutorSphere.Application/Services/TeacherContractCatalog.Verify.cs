using TutorSphere.Application.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public sealed record TeacherContractVerifyChrome(
    string Badge,
    string NotFound,
    string Status,
    string SignedAt,
    string Reference,
    string Hash,
    string Authentic,
    string NotYetSigned);

public static partial class TeacherContractCatalog
{
    public static string PendingHashNotice(string language) =>
        SupportedLanguageCodes.Normalize(language) switch
        {
            SupportedLanguageCodes.English => "pending calculation",
            SupportedLanguageCodes.Spanish => "cálculo en curso",
            SupportedLanguageCodes.German => "Berechnung läuft",
            SupportedLanguageCodes.Portuguese => "cálculo em curso",
            SupportedLanguageCodes.MandarinChinese => "计算中",
            SupportedLanguageCodes.Arabic => "جارٍ الحساب",
            _ => "en cours de calcul"
        };

    public static string SignPadRequired(string language) =>
        SupportedLanguageCodes.Normalize(language) switch
        {
            SupportedLanguageCodes.English => "Please add your electronic signature.",
            SupportedLanguageCodes.Spanish => "Añada su firma electrónica.",
            SupportedLanguageCodes.German => "Bitte fügen Sie Ihre elektronische Signatur hinzu.",
            SupportedLanguageCodes.Portuguese => "Adicione a sua assinatura eletrónica.",
            SupportedLanguageCodes.MandarinChinese => "请添加您的电子签名。",
            SupportedLanguageCodes.Arabic => "يرجى إضافة توقيعك الإلكتروني.",
            _ => "Apposez votre signature électronique."
        };

    public static string InvalidOrExpiredLink(string language) =>
        SupportedLanguageCodes.Normalize(language) switch
        {
            SupportedLanguageCodes.English => "Invalid or expired link.",
            SupportedLanguageCodes.Spanish => "Enlace no válido o caducado.",
            SupportedLanguageCodes.German => "Ungültiger oder abgelaufener Link.",
            SupportedLanguageCodes.Portuguese => "Ligação inválida ou expirada.",
            SupportedLanguageCodes.MandarinChinese => "链接无效或已过期。",
            SupportedLanguageCodes.Arabic => "الرابط غير صالح أو منتهٍ.",
            _ => "Lien invalide ou expiré."
        };

    public static string RequestFailed(string language) =>
        SupportedLanguageCodes.Normalize(language) switch
        {
            SupportedLanguageCodes.English => "The request failed.",
            SupportedLanguageCodes.Spanish => "La solicitud ha fallado.",
            SupportedLanguageCodes.German => "Die Anfrage ist fehlgeschlagen.",
            SupportedLanguageCodes.Portuguese => "O pedido falhou.",
            SupportedLanguageCodes.MandarinChinese => "请求失败。",
            SupportedLanguageCodes.Arabic => "فشل الطلب.",
            _ => "La requête a échoué."
        };

    public static TeacherContractVerifyChrome VerifyChrome(string language) =>
        SupportedLanguageCodes.Normalize(language) switch
        {
            SupportedLanguageCodes.English => new(
                "Authenticity verification",
                "No contract found for this number.",
                "Status",
                "Signed on",
                "Reference",
                "SHA-256 fingerprint",
                "Authentic document recorded by TutorSphere.",
                "This contract is not (yet) a signed original."),
            SupportedLanguageCodes.Spanish => new(
                "Verificación de autenticidad",
                "No se encontró ningún contrato para este número.",
                "Estado",
                "Firmado el",
                "Referencia",
                "Huella SHA-256",
                "Documento auténtico registrado por TutorSphere.",
                "Este contrato no es (aún) un original firmado."),
            SupportedLanguageCodes.German => new(
                "Echtheitsprüfung",
                "Kein Vertrag für diese Nummer gefunden.",
                "Status",
                "Unterzeichnet am",
                "Referenz",
                "SHA-256-Prüfsumme",
                "Authentisches Dokument, erfasst von TutorSphere.",
                "Dieser Vertrag ist (noch) kein unterzeichnetes Original."),
            SupportedLanguageCodes.Portuguese => new(
                "Verificação de autenticidade",
                "Nenhum contrato encontrado para este número.",
                "Estado",
                "Assinado em",
                "Referência",
                "Impressão SHA-256",
                "Documento autêntico registado pela TutorSphere.",
                "Este contrato ainda não é um original assinado."),
            SupportedLanguageCodes.MandarinChinese => new(
                "真实性核验",
                "未找到该编号的合同。",
                "状态",
                "签署于",
                "编号",
                "SHA-256 指纹",
                "TutorSphere 已登记的真实文件。",
                "本合同尚不是已签署的原件。"),
            SupportedLanguageCodes.Arabic => new(
                "التحقق من الأصالة",
                "لم يُعثر على عقد بهذا الرقم.",
                "الحالة",
                "وُقّع في",
                "المرجع",
                "بصمة SHA-256",
                "وثيقة أصيلة مسجَّلة لدى TutorSphere.",
                "هذا العقد ليس (بعد) أصلًا موقَّعًا."),
            _ => new(
                "Vérification d’authenticité",
                "Aucun contrat trouvé pour ce numéro.",
                "Statut",
                "Signé le",
                "Référence",
                "Empreinte SHA-256",
                "Document authentique enregistré par TutorSphere.",
                "Ce contrat n’est pas (encore) un original signé.")
        };

    public static string ContractStatusLabel(string language, TeacherContractStatus status)
    {
        var lang = SupportedLanguageCodes.Normalize(language);
        return (lang, status) switch
        {
            (SupportedLanguageCodes.English, TeacherContractStatus.Draft) => "Draft",
            (SupportedLanguageCodes.English, TeacherContractStatus.Sent) => "Sent",
            (SupportedLanguageCodes.English, TeacherContractStatus.Viewed) => "Viewed",
            (SupportedLanguageCodes.English, TeacherContractStatus.AwaitingSignature) => "Awaiting signature",
            (SupportedLanguageCodes.English, TeacherContractStatus.Signed) => "Signed",
            (SupportedLanguageCodes.English, TeacherContractStatus.Expired) => "Expired",
            (SupportedLanguageCodes.English, TeacherContractStatus.Refused) => "Refused",
            (SupportedLanguageCodes.English, TeacherContractStatus.Cancelled) => "Cancelled",
            (SupportedLanguageCodes.English, TeacherContractStatus.Replaced) => "Replaced",
            (SupportedLanguageCodes.Spanish, TeacherContractStatus.Draft) => "Borrador",
            (SupportedLanguageCodes.Spanish, TeacherContractStatus.Sent) => "Enviado",
            (SupportedLanguageCodes.Spanish, TeacherContractStatus.Viewed) => "Consultado",
            (SupportedLanguageCodes.Spanish, TeacherContractStatus.AwaitingSignature) => "Pendiente de firma",
            (SupportedLanguageCodes.Spanish, TeacherContractStatus.Signed) => "Firmado",
            (SupportedLanguageCodes.Spanish, TeacherContractStatus.Expired) => "Caducado",
            (SupportedLanguageCodes.Spanish, TeacherContractStatus.Refused) => "Rechazado",
            (SupportedLanguageCodes.Spanish, TeacherContractStatus.Cancelled) => "Cancelado",
            (SupportedLanguageCodes.Spanish, TeacherContractStatus.Replaced) => "Reemplazado",
            (SupportedLanguageCodes.German, TeacherContractStatus.Draft) => "Entwurf",
            (SupportedLanguageCodes.German, TeacherContractStatus.Sent) => "Gesendet",
            (SupportedLanguageCodes.German, TeacherContractStatus.Viewed) => "Gelesen",
            (SupportedLanguageCodes.German, TeacherContractStatus.AwaitingSignature) => "Wartet auf Unterschrift",
            (SupportedLanguageCodes.German, TeacherContractStatus.Signed) => "Unterzeichnet",
            (SupportedLanguageCodes.German, TeacherContractStatus.Expired) => "Abgelaufen",
            (SupportedLanguageCodes.German, TeacherContractStatus.Refused) => "Abgelehnt",
            (SupportedLanguageCodes.German, TeacherContractStatus.Cancelled) => "Storniert",
            (SupportedLanguageCodes.German, TeacherContractStatus.Replaced) => "Ersetzt",
            (SupportedLanguageCodes.Portuguese, TeacherContractStatus.Draft) => "Rascunho",
            (SupportedLanguageCodes.Portuguese, TeacherContractStatus.Sent) => "Enviado",
            (SupportedLanguageCodes.Portuguese, TeacherContractStatus.Viewed) => "Consultado",
            (SupportedLanguageCodes.Portuguese, TeacherContractStatus.AwaitingSignature) => "A aguardar assinatura",
            (SupportedLanguageCodes.Portuguese, TeacherContractStatus.Signed) => "Assinado",
            (SupportedLanguageCodes.Portuguese, TeacherContractStatus.Expired) => "Expirado",
            (SupportedLanguageCodes.Portuguese, TeacherContractStatus.Refused) => "Recusado",
            (SupportedLanguageCodes.Portuguese, TeacherContractStatus.Cancelled) => "Cancelado",
            (SupportedLanguageCodes.Portuguese, TeacherContractStatus.Replaced) => "Substituído",
            (SupportedLanguageCodes.MandarinChinese, TeacherContractStatus.Draft) => "草稿",
            (SupportedLanguageCodes.MandarinChinese, TeacherContractStatus.Sent) => "已发送",
            (SupportedLanguageCodes.MandarinChinese, TeacherContractStatus.Viewed) => "已查阅",
            (SupportedLanguageCodes.MandarinChinese, TeacherContractStatus.AwaitingSignature) => "待签署",
            (SupportedLanguageCodes.MandarinChinese, TeacherContractStatus.Signed) => "已签署",
            (SupportedLanguageCodes.MandarinChinese, TeacherContractStatus.Expired) => "已过期",
            (SupportedLanguageCodes.MandarinChinese, TeacherContractStatus.Refused) => "已拒绝",
            (SupportedLanguageCodes.MandarinChinese, TeacherContractStatus.Cancelled) => "已取消",
            (SupportedLanguageCodes.MandarinChinese, TeacherContractStatus.Replaced) => "已替换",
            (SupportedLanguageCodes.Arabic, TeacherContractStatus.Draft) => "مسودة",
            (SupportedLanguageCodes.Arabic, TeacherContractStatus.Sent) => "مُرسَل",
            (SupportedLanguageCodes.Arabic, TeacherContractStatus.Viewed) => "تم الاطلاع",
            (SupportedLanguageCodes.Arabic, TeacherContractStatus.AwaitingSignature) => "بانتظار التوقيع",
            (SupportedLanguageCodes.Arabic, TeacherContractStatus.Signed) => "موقَّع",
            (SupportedLanguageCodes.Arabic, TeacherContractStatus.Expired) => "منتهٍ",
            (SupportedLanguageCodes.Arabic, TeacherContractStatus.Refused) => "مرفوض",
            (SupportedLanguageCodes.Arabic, TeacherContractStatus.Cancelled) => "ملغى",
            (SupportedLanguageCodes.Arabic, TeacherContractStatus.Replaced) => "مُستبدل",
            (_, TeacherContractStatus.Draft) => "Brouillon",
            (_, TeacherContractStatus.Sent) => "Envoyé",
            (_, TeacherContractStatus.Viewed) => "Consulté",
            (_, TeacherContractStatus.AwaitingSignature) => "En attente de signature",
            (_, TeacherContractStatus.Signed) => "Signé",
            (_, TeacherContractStatus.Expired) => "Expiré",
            (_, TeacherContractStatus.Refused) => "Refusé",
            (_, TeacherContractStatus.Cancelled) => "Annulé",
            (_, TeacherContractStatus.Replaced) => "Remplacé par une nouvelle version",
            _ => status.ToString()
        };
    }
}
