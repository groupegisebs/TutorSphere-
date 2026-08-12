import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");

const entries = {
  ExpertTeachers_Title: {
    fr: "Suivi des enseignants",
    en: "Teacher monitoring",
    es: "Seguimiento de docentes",
    de: "Lehrkräfte-Überwachung",
    pt: "Acompanhamento de professores",
    "zh-Hans": "教师跟踪",
    ar: "تتبع المعلمين",
  },
  ExpertTeachers_Subtitle: {
    fr: "Activité, supports de cours et remarques pour les enseignants approuvés par votre groupe.",
    en: "Activity, course materials and remarks for teachers approved by your group.",
    es: "Actividad, materiales de curso y observaciones de los docentes aprobados por su grupo.",
    de: "Aktivität, Kursmaterialien und Anmerkungen für von Ihrer Gruppe genehmigte Lehrkräfte.",
    pt: "Atividade, materiais de curso e observações dos professores aprovados pelo seu grupo.",
    "zh-Hans": "查看本组已批准教师的活动、课程材料与评语。",
    ar: "النشاط ومواد الدورات والملاحظات للمعلمين الذين وافقت عليهم مجموعتك.",
  },
  ExpertTeachers_Empty: {
    fr: "Aucun enseignant approuvé pour le moment dans votre groupe.",
    en: "No approved teachers in your group yet.",
    es: "Todavía no hay docentes aprobados en su grupo.",
    de: "In Ihrer Gruppe sind noch keine Lehrkräfte genehmigt.",
    pt: "Ainda não há professores aprovados no seu grupo.",
    "zh-Hans": "您所在的专家组目前还没有已批准的教师。",
    ar: "لا يوجد معلمون معتمدون في مجموعتك حتى الآن.",
  },
  ExpertTeachers_ColLessons: {
    fr: "Cours réalisés",
    en: "Lessons given",
    es: "Clases realizadas",
    de: "Gehaltene Stunden",
    pt: "Aulas realizadas",
    "zh-Hans": "已完成课程",
    ar: "الحصص المنجزة",
  },
  ExpertTeachers_ColCancelled: {
    fr: "Annulations",
    en: "Cancellations",
    es: "Cancelaciones",
    de: "Absagen",
    pt: "Cancelamentos",
    "zh-Hans": "取消次数",
    ar: "الإلغاءات",
  },
  ExpertTeachers_ColNoShow: {
    fr: "Absences moniteur",
    en: "Tutor no-shows",
    es: "Ausencias del tutor",
    de: "Nichterscheinen der Lehrkraft",
    pt: "Ausências do professor",
    "zh-Hans": "教师缺席次数",
    ar: "غياب المعلم",
  },
  ExpertTeachers_ColLastActivity: {
    fr: "Dernière activité",
    en: "Last activity",
    es: "Última actividad",
    de: "Letzte Aktivität",
    pt: "Última atividade",
    "zh-Hans": "最近活动",
    ar: "آخر نشاط",
  },
  ExpertTeachers_ColRemarks: {
    fr: "Remarques",
    en: "Remarks",
    es: "Observaciones",
    de: "Anmerkungen",
    pt: "Observações",
    "zh-Hans": "评语",
    ar: "الملاحظات",
  },
  ExpertTeachers_Manage: {
    fr: "Suivre",
    en: "Monitor",
    es: "Seguir",
    de: "Verfolgen",
    pt: "Acompanhar",
    "zh-Hans": "跟踪",
    ar: "تتبع",
  },
  ExpertTeachers_Close: {
    fr: "Fermer",
    en: "Close",
    es: "Cerrar",
    de: "Schließen",
    pt: "Fechar",
    "zh-Hans": "关闭",
    ar: "إغلاق",
  },
  ExpertTeachers_MaterialsTitle: {
    fr: "Supports de cours",
    en: "Course materials",
    es: "Materiales de curso",
    de: "Kursmaterialien",
    pt: "Materiais de curso",
    "zh-Hans": "课程材料",
    ar: "مواد الدورة",
  },
  ExpertTeachers_MaterialsEmpty: {
    fr: "Aucun support de cours créé par cet enseignant pour le moment.",
    en: "No course materials created by this teacher yet.",
    es: "Este docente todavía no ha creado materiales de curso.",
    de: "Diese Lehrkraft hat noch keine Kursmaterialien erstellt.",
    pt: "Este professor ainda não criou materiais de curso.",
    "zh-Hans": "该教师尚未创建任何课程材料。",
    ar: "لم يقم هذا المعلم بإنشاء أي مواد للدورة حتى الآن.",
  },
  ExpertTeachers_RemarksTitle: {
    fr: "Historique des remarques",
    en: "Remark history",
    es: "Historial de observaciones",
    de: "Verlauf der Anmerkungen",
    pt: "Histórico de observações",
    "zh-Hans": "评语历史",
    ar: "سجل الملاحظات",
  },
  ExpertTeachers_RemarksEmpty: {
    fr: "Aucune remarque envoyée à cet enseignant pour le moment.",
    en: "No remarks sent to this teacher yet.",
    es: "Todavía no se han enviado observaciones a este docente.",
    de: "Dieser Lehrkraft wurden noch keine Anmerkungen gesendet.",
    pt: "Ainda não foram enviadas observações a este professor.",
    "zh-Hans": "尚未向该教师发送任何评语。",
    ar: "لم تُرسل أي ملاحظات لهذا المعلم حتى الآن.",
  },
  ExpertTeachers_AddRemarkTitle: {
    fr: "Laisser une remarque",
    en: "Leave a remark",
    es: "Dejar una observación",
    de: "Anmerkung hinzufügen",
    pt: "Deixar uma observação",
    "zh-Hans": "留下评语",
    ar: "إضافة ملاحظة",
  },
  ExpertTeachers_RemarkCategory: {
    fr: "Catégorie",
    en: "Category",
    es: "Categoría",
    de: "Kategorie",
    pt: "Categoria",
    "zh-Hans": "类别",
    ar: "الفئة",
  },
  ExpertTeachers_RemarkCategory_General: {
    fr: "Général",
    en: "General",
    es: "General",
    de: "Allgemein",
    pt: "Geral",
    "zh-Hans": "综合",
    ar: "عام",
  },
  ExpertTeachers_RemarkCategory_Activity: {
    fr: "Activité",
    en: "Activity",
    es: "Actividad",
    de: "Aktivität",
    pt: "Atividade",
    "zh-Hans": "活动",
    ar: "النشاط",
  },
  ExpertTeachers_RemarkCategory_CourseMaterial: {
    fr: "Support de cours",
    en: "Course material",
    es: "Material de curso",
    de: "Kursmaterial",
    pt: "Material de curso",
    "zh-Hans": "课程材料",
    ar: "مادة الدورة",
  },
  ExpertTeachers_RelatedMaterial: {
    fr: "Support lié (optionnel)",
    en: "Related material (optional)",
    es: "Material relacionado (opcional)",
    de: "Zugehöriges Material (optional)",
    pt: "Material relacionado (opcional)",
    "zh-Hans": "关联材料（可选）",
    ar: "مادة مرتبطة (اختياري)",
  },
  ExpertTeachers_RelatedMaterialNone: {
    fr: "Aucun (remarque générale)",
    en: "None (general remark)",
    es: "Ninguno (observación general)",
    de: "Keines (allgemeine Anmerkung)",
    pt: "Nenhum (observação geral)",
    "zh-Hans": "无（综合评语）",
    ar: "بلا (ملاحظة عامة)",
  },
  ExpertTeachers_RemarkMessage: {
    fr: "Message",
    en: "Message",
    es: "Mensaje",
    de: "Nachricht",
    pt: "Mensagem",
    "zh-Hans": "留言内容",
    ar: "الرسالة",
  },
  ExpertTeachers_RemarkMessagePlaceholder: {
    fr: "Décrivez votre observation ou votre recommandation…",
    en: "Describe your observation or recommendation…",
    es: "Describa su observación o recomendación…",
    de: "Beschreiben Sie Ihre Beobachtung oder Empfehlung…",
    pt: "Descreva a sua observação ou recomendação…",
    "zh-Hans": "请描述您的意见或建议……",
    ar: "صِف ملاحظتك أو توصيتك…",
  },
  ExpertTeachers_SendRemark: {
    fr: "Envoyer la remarque",
    en: "Send remark",
    es: "Enviar observación",
    de: "Anmerkung senden",
    pt: "Enviar observação",
    "zh-Hans": "发送评语",
    ar: "إرسال الملاحظة",
  },
  ExpertTeachers_RemarkSent: {
    fr: "Remarque envoyée à l'enseignant.",
    en: "Remark sent to the teacher.",
    es: "Observación enviada al docente.",
    de: "Anmerkung an die Lehrkraft gesendet.",
    pt: "Observação enviada ao professor.",
    "zh-Hans": "评语已发送给该教师。",
    ar: "تم إرسال الملاحظة إلى المعلم.",
  },
  ExpertTeachers_RemarkFailed: {
    fr: "Échec de l'envoi de la remarque.",
    en: "Failed to send the remark.",
    es: "No se pudo enviar la observación.",
    de: "Anmerkung konnte nicht gesendet werden.",
    pt: "Falha ao enviar a observação.",
    "zh-Hans": "评语发送失败。",
    ar: "فشل إرسال الملاحظة.",
  },
  ExpertTeachers_RemarkMessageRequired: {
    fr: "Le message de la remarque est requis.",
    en: "The remark message is required.",
    es: "El mensaje de la observación es obligatorio.",
    de: "Der Text der Anmerkung ist erforderlich.",
    pt: "A mensagem da observação é obrigatória.",
    "zh-Hans": "请填写评语内容。",
    ar: "رسالة الملاحظة مطلوبة.",
  },
  ExpertTeachers_MaterialKind_Homework: {
    fr: "Devoir",
    en: "Homework",
    es: "Tarea",
    de: "Hausaufgabe",
    pt: "Trabalho de casa",
    "zh-Hans": "作业",
    ar: "واجب",
  },
  ExpertTeachers_MaterialKind_Document: {
    fr: "Document",
    en: "Document",
    es: "Documento",
    de: "Dokument",
    pt: "Documento",
    "zh-Hans": "文档",
    ar: "وثيقة",
  },
  TutorExpertRemarks_Title: {
    fr: "Remarques des experts",
    en: "Expert remarks",
    es: "Observaciones de expertos",
    de: "Anmerkungen der Experten",
    pt: "Observações dos especialistas",
    "zh-Hans": "专家评语",
    ar: "ملاحظات الخبراء",
  },
  TutorExpertRemarks_Subtitle: {
    fr: "Suivi qualité de votre établissement par le groupe d'experts responsable.",
    en: "Quality follow-up of your school by the responsible expert group.",
    es: "Seguimiento de calidad de su centro por el grupo de expertos responsable.",
    de: "Qualitätsüberwachung Ihrer Schule durch die zuständige Expertengruppe.",
    pt: "Acompanhamento de qualidade da sua escola pelo grupo de especialistas responsável.",
    "zh-Hans": "由负责的专家组对您的机构进行的质量跟进。",
    ar: "متابعة جودة مؤسستك من قبل مجموعة الخبراء المسؤولة.",
  },
  TutorExpertRemarks_Empty: {
    fr: "Vous n'avez reçu aucune remarque d'expert pour le moment.",
    en: "You haven't received any expert remarks yet.",
    es: "Todavía no ha recibido ninguna observación de un experto.",
    de: "Sie haben noch keine Anmerkungen von Experten erhalten.",
    pt: "Ainda não recebeu nenhuma observação de um especialista.",
    "zh-Hans": "您尚未收到任何专家评语。",
    ar: "لم تستلم أي ملاحظات من الخبراء حتى الآن.",
  },
  TutorExpertRemarks_New: {
    fr: "Nouveau",
    en: "New",
    es: "Nuevo",
    de: "Neu",
    pt: "Novo",
    "zh-Hans": "新",
    ar: "جديد",
  },
  TutorDashboard_ExpertRemarksTitle: {
    fr: "Remarques des experts",
    en: "Expert remarks",
    es: "Observaciones de expertos",
    de: "Anmerkungen der Experten",
    pt: "Observações dos especialistas",
    "zh-Hans": "专家评语",
    ar: "ملاحظات الخبراء",
  },
  TutorDashboard_ExpertRemarksEmpty: {
    fr: "Aucune remarque d'expert pour le moment.",
    en: "No expert remarks yet.",
    es: "Todavía no hay observaciones de expertos.",
    de: "Noch keine Anmerkungen von Experten.",
    pt: "Ainda não há observações de especialistas.",
    "zh-Hans": "暂无专家评语。",
    ar: "لا توجد ملاحظات من الخبراء حتى الآن.",
  },
  Nav_ExpertRemarks: {
    fr: "Remarques experts",
    en: "Expert remarks",
    es: "Observaciones de expertos",
    de: "Experten-Anmerkungen",
    pt: "Observações de especialistas",
    "zh-Hans": "专家评语",
    ar: "ملاحظات الخبراء",
  },
};

function parseResx(text) {
  const map = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(text))) {
    map[m[1]] = m[2]
      .replace(/&lt;/g, "<")
      .replace(/&gt;/g, ">")
      .replace(/&amp;/g, "&");
  }
  return map;
}

function esc(v) {
  return String(v)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

function buildResx(pairs) {
  const keys = Object.keys(pairs).sort((a, b) =>
    a.localeCompare(b, "en", { sensitivity: "base" })
  );
  const body = keys
    .map(
      (k) =>
        `  <data name="${k}" xml:space="preserve">\r\n    <value>${esc(pairs[k])}</value>\r\n  </data>`
    )
    .join("\r\n");
  return (
    `<?xml version="1.0" encoding="utf-8"?>\r\n` +
    `<root>\r\n` +
    `  <resheader name="resmimetype">\r\n` +
    `    <value>text/microsoft-resx</value>\r\n` +
    `  </resheader>\r\n` +
    `  <resheader name="version">\r\n` +
    `    <value>1.3</value>\r\n` +
    `  </resheader>\r\n` +
    `${body}\r\n` +
    `</root>\r\n`
  );
}

const files = {
  "": "SharedResources.resx",
  fr: "SharedResources.fr.resx",
  en: "SharedResources.en.resx",
  es: "SharedResources.es.resx",
  de: "SharedResources.de.resx",
  pt: "SharedResources.pt.resx",
  "zh-Hans": "SharedResources.zh-Hans.resx",
  ar: "SharedResources.ar.resx",
};

for (const [lang, file] of Object.entries(files)) {
  const p = path.join(resDir, file);
  const map = parseResx(fs.readFileSync(p, "utf8"));
  const locale = lang === "" ? "fr" : lang;
  for (const [key, tr] of Object.entries(entries)) {
    map[key] = tr[locale] ?? tr.en;
  }
  fs.writeFileSync(p, buildResx(map), "utf8");
  console.log("updated", file);
}
