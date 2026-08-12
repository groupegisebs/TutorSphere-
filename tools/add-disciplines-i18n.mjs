import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const resDir = path.join(__dirname, "../src/TutorSphere.Web/Resources");

const entries = {
  ExpertDisciplines_Title: {
    fr: "Disciplines",
    en: "Disciplines",
    es: "Disciplinas",
    de: "Fachbereiche",
    pt: "Disciplinas",
    "zh-Hans": "学科",
    ar: "التخصصات",
  },
  ExpertDisciplines_Subtitle: {
    fr: "Définissez les disciplines de votre groupe par cycle scolaire, les services fournis, la méthode de travail, puis affectez vos enseignants.",
    en: "Define your group's disciplines by school cycle, the services provided, the work method, then assign your teachers.",
    es: "Defina las disciplinas de su grupo por ciclo escolar, los servicios ofrecidos, el método de trabajo y luego asigne a sus docentes.",
    de: "Definieren Sie die Fachbereiche Ihrer Gruppe nach Schulstufe, die angebotenen Leistungen und die Arbeitsmethode, und weisen Sie dann Ihre Lehrkräfte zu.",
    pt: "Defina as disciplinas do seu grupo por ciclo escolar, os serviços prestados, o método de trabalho e depois atribua os seus professores.",
    "zh-Hans": "按学段定义您所在专家组的学科、提供的服务与工作方法，然后指派教师。",
    ar: "حدد تخصصات مجموعتك حسب المرحلة الدراسية، والخدمات المقدمة، وطريقة العمل، ثم عيّن معلميك.",
  },
  ExpertDisciplines_New: {
    fr: "Nouvelle discipline",
    en: "New discipline",
    es: "Nueva disciplina",
    de: "Neuer Fachbereich",
    pt: "Nova disciplina",
    "zh-Hans": "新建学科",
    ar: "تخصص جديد",
  },
  ExpertDisciplines_CreateTitle: {
    fr: "Créer une discipline",
    en: "Create a discipline",
    es: "Crear una disciplina",
    de: "Fachbereich erstellen",
    pt: "Criar uma disciplina",
    "zh-Hans": "创建学科",
    ar: "إنشاء تخصص",
  },
  ExpertDisciplines_EditTitle: {
    fr: "Modifier la discipline",
    en: "Edit discipline",
    es: "Editar disciplina",
    de: "Fachbereich bearbeiten",
    pt: "Editar disciplina",
    "zh-Hans": "编辑学科",
    ar: "تعديل التخصص",
  },
  ExpertDisciplines_Name: {
    fr: "Nom de la discipline",
    en: "Discipline name",
    es: "Nombre de la disciplina",
    de: "Name des Fachbereichs",
    pt: "Nome da disciplina",
    "zh-Hans": "学科名称",
    ar: "اسم التخصص",
  },
  ExpertDisciplines_NamePlaceholder: {
    fr: "Ex. Mathématiques, Français, Physique…",
    en: "E.g. Mathematics, French, Physics…",
    es: "P. ej. Matemáticas, Francés, Física…",
    de: "z. B. Mathematik, Französisch, Physik…",
    pt: "Ex. Matemática, Francês, Física…",
    "zh-Hans": "例如：数学、法语、物理……",
    ar: "مثل: الرياضيات، الفرنسية، الفيزياء…",
  },
  ExpertDisciplines_Cycle: {
    fr: "Cycle scolaire",
    en: "School cycle",
    es: "Ciclo escolar",
    de: "Schulstufe",
    pt: "Ciclo escolar",
    "zh-Hans": "学段",
    ar: "المرحلة الدراسية",
  },
  ExpertDisciplines_WorkMethod: {
    fr: "Méthode de travail",
    en: "Work method",
    es: "Método de trabajo",
    de: "Arbeitsmethode",
    pt: "Método de trabalho",
    "zh-Hans": "工作方法",
    ar: "طريقة العمل",
  },
  ExpertDisciplines_WorkMethodHint: {
    fr: "Décrivez précisément comment vous accompagnez les élèves/étudiants ayant des besoins dans cette discipline (ce n'est pas une école, mais un service d'accompagnement).",
    en: "Describe precisely how you support students with needs in this discipline (this is not a school, but a support service).",
    es: "Describa con precisión cómo acompaña a los alumnos/estudiantes con necesidades en esta disciplina (no es una escuela, sino un servicio de acompañamiento).",
    de: "Beschreiben Sie genau, wie Sie Schüler/Studierende mit Bedarf in diesem Fachbereich unterstützen (dies ist keine Schule, sondern ein Unterstützungsdienst).",
    pt: "Descreva com precisão como acompanha os alunos/estudantes com necessidades nesta disciplina (não é uma escola, mas um serviço de acompanhamento).",
    "zh-Hans": "请具体说明您在该学科中如何为有需要的学生提供支持（这不是一所学校，而是一项陪伴服务）。",
    ar: "صِف بدقة كيف تدعم الطلاب ذوي الاحتياجات في هذا التخصص (هذه ليست مدرسة، بل خدمة مواكبة).",
  },
  ExpertDisciplines_WorkMethodPlaceholder: {
    fr: "Ex. suivi individualisé hebdomadaire, plan d'accompagnement personnalisé, points de contrôle réguliers…",
    en: "E.g. weekly individualized follow-up, personalized support plan, regular check-ins…",
    es: "P. ej. seguimiento individualizado semanal, plan de acompañamiento personalizado, puntos de control regulares…",
    de: "z. B. wöchentliche individuelle Betreuung, personalisierter Unterstützungsplan, regelmäßige Kontrollpunkte…",
    pt: "Ex. acompanhamento individualizado semanal, plano de apoio personalizado, pontos de controlo regulares…",
    "zh-Hans": "例如：每周个别跟进、个性化支持方案、定期检查点……",
    ar: "مثل: متابعة فردية أسبوعية، خطة دعم مخصصة، نقاط تحقق منتظمة…",
  },
  ExpertDisciplines_Services: {
    fr: "Services fournis",
    en: "Services provided",
    es: "Servicios ofrecidos",
    de: "Angebotene Leistungen",
    pt: "Serviços prestados",
    "zh-Hans": "提供的服务",
    ar: "الخدمات المقدمة",
  },
  ExpertDisciplines_ServicesHint: {
    fr: "Listez chaque service en détail (ex. soutien aux devoirs, préparation aux examens, suivi personnalisé).",
    en: "List each service in detail (e.g. homework support, exam prep, personalized follow-up).",
    es: "Detalle cada servicio (p. ej. apoyo con las tareas, preparación de examenes, seguimiento personalizado).",
    de: "Listen Sie jede Leistung detailliert auf (z. B. Hausaufgabenhilfe, Prüfungsvorbereitung, individuelle Betreuung).",
    pt: "Liste cada serviço em detalhe (ex. apoio nos deveres, preparação para exames, acompanhamento personalizado).",
    "zh-Hans": "详细列出每项服务（例如：作业辅导、考试备考、个性化跟进）。",
    ar: "اذكر كل خدمة بالتفصيل (مثل: دعم الواجبات، التحضير للاختبارات، المتابعة الشخصية).",
  },
  ExpertDisciplines_AddService: {
    fr: "Ajouter un service",
    en: "Add a service",
    es: "Añadir un servicio",
    de: "Leistung hinzufügen",
    pt: "Adicionar um serviço",
    "zh-Hans": "添加服务",
    ar: "إضافة خدمة",
  },
  ExpertDisciplines_NoServicesYet: {
    fr: "Aucun service ajouté pour le moment.",
    en: "No services added yet.",
    es: "Todavía no se ha añadido ningún servicio.",
    de: "Noch keine Leistungen hinzugefügt.",
    pt: "Ainda não foi adicionado nenhum serviço.",
    "zh-Hans": "尚未添加任何服务。",
    ar: "لم تُضَف أي خدمة حتى الآن.",
  },
  ExpertDisciplines_ServiceTitlePlaceholder: {
    fr: "Titre du service (ex. Soutien aux devoirs)",
    en: "Service title (e.g. Homework support)",
    es: "Título del servicio (p. ej. Apoyo con las tareas)",
    de: "Titel der Leistung (z. B. Hausaufgabenhilfe)",
    pt: "Título do serviço (ex. Apoio nos deveres)",
    "zh-Hans": "服务名称（例如：作业辅导）",
    ar: "عنوان الخدمة (مثل: دعم الواجبات)",
  },
  ExpertDisciplines_ServiceDescPlaceholder: {
    fr: "Décrivez ce service en détail…",
    en: "Describe this service in detail…",
    es: "Describa este servicio en detalle…",
    de: "Beschreiben Sie diese Leistung im Detail…",
    pt: "Descreva este serviço em detalhe…",
    "zh-Hans": "请详细描述该服务……",
    ar: "صِف هذه الخدمة بالتفصيل…",
  },
  ExpertDisciplines_Empty: {
    fr: "Aucune discipline définie pour le moment. Créez-en une pour commencer à affecter vos enseignants.",
    en: "No discipline defined yet. Create one to start assigning your teachers.",
    es: "Todavía no se ha definido ninguna disciplina. Cree una para empezar a asignar a sus docentes.",
    de: "Es wurde noch kein Fachbereich definiert. Erstellen Sie einen, um Ihre Lehrkräfte zuzuweisen.",
    pt: "Ainda não foi definida nenhuma disciplina. Crie uma para começar a atribuir os seus professores.",
    "zh-Hans": "尚未定义任何学科。请创建一个学科以开始指派教师。",
    ar: "لم يُحدَّد أي تخصص حتى الآن. أنشئ تخصصًا للبدء في تعيين معلميك.",
  },
  ExpertDisciplines_ColTeachers: {
    fr: "Enseignants affectés",
    en: "Assigned teachers",
    es: "Docentes asignados",
    de: "Zugewiesene Lehrkräfte",
    pt: "Professores atribuídos",
    "zh-Hans": "已指派教师",
    ar: "المعلمون المعيّنون",
  },
  ExpertDisciplines_ManageTeachers: {
    fr: "Gérer les enseignants",
    en: "Manage teachers",
    es: "Gestionar docentes",
    de: "Lehrkräfte verwalten",
    pt: "Gerir professores",
    "zh-Hans": "管理教师",
    ar: "إدارة المعلمين",
  },
  ExpertDisciplines_TeachersFor: {
    fr: "Enseignants affectés",
    en: "Assigned teachers",
    es: "Docentes asignados",
    de: "Zugewiesene Lehrkräfte",
    pt: "Professores atribuídos",
    "zh-Hans": "已指派教师",
    ar: "المعلمون المعيّنون",
  },
  ExpertDisciplines_NoApprovedTeachers: {
    fr: "Aucun enseignant approuvé dans votre groupe pour le moment.",
    en: "No approved teachers in your group yet.",
    es: "Todavía no hay docentes aprobados en su grupo.",
    de: "In Ihrer Gruppe sind noch keine Lehrkräfte genehmigt.",
    pt: "Ainda não há professores aprovados no seu grupo.",
    "zh-Hans": "您所在的专家组目前还没有已批准的教师。",
    ar: "لا يوجد معلمون معتمدون في مجموعتك حتى الآن.",
  },
  ExpertDisciplines_NameRequired: {
    fr: "Le nom de la discipline est requis.",
    en: "The discipline name is required.",
    es: "El nombre de la disciplina es obligatorio.",
    de: "Der Name des Fachbereichs ist erforderlich.",
    pt: "O nome da disciplina é obrigatório.",
    "zh-Hans": "请填写学科名称。",
    ar: "اسم التخصص مطلوب.",
  },
  ExpertDisciplines_SaveFailed: {
    fr: "Échec de l'enregistrement.",
    en: "Failed to save.",
    es: "No se pudo guardar.",
    de: "Speichern fehlgeschlagen.",
    pt: "Falha ao guardar.",
    "zh-Hans": "保存失败。",
    ar: "فشل الحفظ.",
  },
  ExpertDisciplines_Created: {
    fr: "Discipline créée.",
    en: "Discipline created.",
    es: "Disciplina creada.",
    de: "Fachbereich erstellt.",
    pt: "Disciplina criada.",
    "zh-Hans": "学科已创建。",
    ar: "تم إنشاء التخصص.",
  },
  ExpertDisciplines_Updated: {
    fr: "Discipline mise à jour.",
    en: "Discipline updated.",
    es: "Disciplina actualizada.",
    de: "Fachbereich aktualisiert.",
    pt: "Disciplina atualizada.",
    "zh-Hans": "学科已更新。",
    ar: "تم تحديث التخصص.",
  },
  ExpertDisciplines_ConfirmDelete: {
    fr: "Supprimer définitivement la discipline « {0} » ? Cette action est irréversible.",
    en: "Permanently delete the discipline \"{0}\"? This action cannot be undone.",
    es: "¿Eliminar definitivamente la disciplina «{0}»? Esta acción no se puede deshacer.",
    de: "Fachbereich „{0}“ endgültig löschen? Diese Aktion kann nicht widerrufen werden.",
    pt: "Eliminar definitivamente a disciplina «{0}»? Esta ação é irreversível.",
    "zh-Hans": "确定永久删除学科“{0}”吗？此操作无法撤销。",
    ar: "هل تريد حذف التخصص «{0}» نهائيًا؟ لا يمكن التراجع عن هذا الإجراء.",
  },
  ExpertDisciplines_DeleteFailed: {
    fr: "Suppression impossible.",
    en: "Unable to delete.",
    es: "No se pudo eliminar.",
    de: "Löschen nicht möglich.",
    pt: "Não foi possível eliminar.",
    "zh-Hans": "无法删除。",
    ar: "تعذّر الحذف.",
  },
  ExpertDisciplines_Deleted: {
    fr: "Discipline supprimée.",
    en: "Discipline deleted.",
    es: "Disciplina eliminada.",
    de: "Fachbereich gelöscht.",
    pt: "Disciplina eliminada.",
    "zh-Hans": "学科已删除。",
    ar: "تم حذف التخصص.",
  },
  SchoolCycle_Primary: {
    fr: "Primaire",
    en: "Primary",
    es: "Primaria",
    de: "Grundschule",
    pt: "Primário",
    "zh-Hans": "小学",
    ar: "الابتدائي",
  },
  SchoolCycle_Secondary: {
    fr: "Secondaire",
    en: "Secondary",
    es: "Secundaria",
    de: "Sekundarstufe",
    pt: "Secundário",
    "zh-Hans": "中学",
    ar: "الثانوي",
  },
  SchoolCycle_University: {
    fr: "Universitaire",
    en: "University",
    es: "Universitaria",
    de: "Universität",
    pt: "Universitário",
    "zh-Hans": "大学",
    ar: "الجامعي",
  },
  SchoolCycle_AdultEducation: {
    fr: "Formation pour adultes",
    en: "Adult education",
    es: "Formación para adultos",
    de: "Erwachsenenbildung",
    pt: "Formação para adultos",
    "zh-Hans": "成人教育",
    ar: "تعليم الكبار",
  },
  PublicProfile_Tab_Disciplines: {
    fr: "Disciplines",
    en: "Disciplines",
    es: "Disciplinas",
    de: "Fachbereiche",
    pt: "Disciplinas",
    "zh-Hans": "学科",
    ar: "التخصصات",
  },
  PublicProfile_NoDisciplines: {
    fr: "Aucune discipline publiée pour le moment.",
    en: "No discipline published yet.",
    es: "Todavía no se ha publicado ninguna disciplina.",
    de: "Noch kein Fachbereich veröffentlicht.",
    pt: "Ainda não foi publicada nenhuma disciplina.",
    "zh-Hans": "尚未发布任何学科信息。",
    ar: "لم يُنشر أي تخصص حتى الآن.",
  },
  PublicProfile_DisciplineServices: {
    fr: "Services fournis",
    en: "Services provided",
    es: "Servicios ofrecidos",
    de: "Angebotene Leistungen",
    pt: "Serviços prestados",
    "zh-Hans": "提供的服务",
    ar: "الخدمات المقدمة",
  },
  PublicProfile_DisciplineWorkMethod: {
    fr: "Méthode de travail",
    en: "Work method",
    es: "Método de trabajo",
    de: "Arbeitsmethode",
    pt: "Método de trabalho",
    "zh-Hans": "工作方法",
    ar: "طريقة العمل",
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
