import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const KEYS = {
  TeacherApply_Title: {
    fr: "Candidature enseignant",
    en: "Teacher application",
    es: "Candidatura docente",
    de: "Lehrerbewerbung",
    pt: "Candidatura de professor",
    zh: "教师申请",
    ar: "ترشيح معلم"
  },
  TeacherApply_Badge: {
    fr: "Espace Enseignant",
    en: "Teacher space",
    es: "Espacio docente",
    de: "Lehrerbereich",
    pt: "Espaço professor",
    zh: "教师空间",
    ar: "مساحة المعلم"
  },
  TeacherApply_AGroup: {
    fr: "Un groupe d'experts",
    en: "An expert group",
    es: "Un grupo de expertos",
    de: "Eine Expertengruppe",
    pt: "Um grupo de especialistas",
    zh: "一个专家组",
    ar: "مجموعة خبراء"
  },
  TeacherApply_InvitesYou: {
    fr: "vous invite à déposer votre candidature auprès de",
    en: "invites you to apply to",
    es: "le invita a presentar su candidatura a",
    de: "lädt Sie ein, sich zu bewerben bei",
    pt: "convida-o a candidatar-se a",
    zh: "邀请您向以下小组提交申请",
    ar: "يدعوك لتقديم ترشيحك إلى"
  },
  TeacherApply_PlatformTitle: {
    fr: "TutorSphere pour les enseignants",
    en: "TutorSphere for teachers",
    es: "TutorSphere para docentes",
    de: "TutorSphere für Lehrkräfte",
    pt: "TutorSphere para professores",
    zh: "面向教师的 TutorSphere",
    ar: "TutorSphere للمعلمين"
  },
  TeacherApply_PlatformLead: {
    fr: "TutorSphere relie enseignants, familles et groupes d'experts. Votre dossier est examiné par le groupe qui vous invite avant que vous puissiez donner des cours.",
    en: "TutorSphere connects teachers, families and expert groups. The inviting group reviews your file before you can teach.",
    es: "TutorSphere conecta docentes, familias y grupos de expertos. El grupo que le invita examina su expediente antes de que pueda dar clases.",
    de: "TutorSphere verbindet Lehrkräfte, Familien und Expertengruppen. Die einladende Gruppe prüft Ihre Unterlagen, bevor Sie unterrichten.",
    pt: "O TutorSphere liga professores, famílias e grupos de especialistas. O grupo que o convida analisa o seu dossiê antes de poder dar aulas.",
    zh: "TutorSphere 连接教师、家庭与专家组。邀请您的小组会先审核材料，然后您才能授课。",
    ar: "تربط TutorSphere المعلمين والأسر ومجموعات الخبراء. تراجع المجموعة الداعية ملفك قبل أن تدرّس."
  },
  TeacherApply_PlatformBullet1: {
    fr: "Création d'un compte enseignant TutorSphere à partir de cette invitation nominative.",
    en: "A TutorSphere teacher account is created from this named invitation.",
    es: "Se crea una cuenta docente TutorSphere a partir de esta invitación nominativa.",
    de: "Aus dieser namentlichen Einladung entsteht ein TutorSphere-Lehrkonto.",
    pt: "Uma conta de professor TutorSphere é criada a partir deste convite nominativo.",
    zh: "通过此具名邀请创建 TutorSphere 教师账户。",
    ar: "يُنشأ حساب معلم TutorSphere من هذه الدعوة الاسمية."
  },
  TeacherApply_PlatformBullet2: {
    fr: "Le groupe examine votre profil, vos matières et, selon son processus, un entretien ou une démonstration.",
    en: "The group reviews your profile, subjects and, depending on its process, an interview or a demonstration.",
    es: "El grupo revisa su perfil, materias y, según su proceso, una entrevista o una demostración.",
    de: "Die Gruppe prüft Profil, Fächer und je nach Verfahren ein Gespräch oder eine Demonstration.",
    pt: "O grupo analisa o seu perfil, disciplinas e, consoante o processo, uma entrevista ou demonstração.",
    zh: "小组审核您的资料、学科，并视流程安排面试或试讲。",
    ar: "تراجع المجموعة ملفك وموادك، وحسب مسارها مقابلة أو عرضًا."
  },
  TeacherApply_PlatformBullet3: {
    fr: "Après approbation, vos offres peuvent être publiées auprès des familles du territoire du groupe.",
    en: "After approval, your offers can be published to families in the group's territory.",
    es: "Tras la aprobación, sus ofertas pueden publicarse para las familias del territorio del grupo.",
    de: "Nach der Freigabe können Ihre Angebote den Familien im Gebiet der Gruppe veröffentlicht werden.",
    pt: "Após aprovação, as suas ofertas podem ser publicadas às famílias do território do grupo.",
    zh: "获批后，您的课程可向该组所在地区的家庭发布。",
    ar: "بعد الموافقة يمكن نشر عروضك لأسر منطقة المجموعة."
  },
  TeacherApply_GroupDescriptionEmpty: {
    fr: "Ce groupe d'experts valide les enseignants et pilote les offres pédagogiques de son territoire.",
    en: "This expert group reviews teachers and steers the learning offers for its territory.",
    es: "Este grupo de expertos valida a los docentes y dirige las ofertas de su territorio.",
    de: "Diese Expertengruppe prüft Lehrkräfte und steuert die Angebote ihres Gebiets.",
    pt: "Este grupo de especialistas valida professores e conduz as ofertas do seu território.",
    zh: "该专家组负责审核教师并管理本地区课程。",
    ar: "تدقق مجموعة الخبراء هذه في المعلمين وتدير عروض منطقتها."
  },
  TeacherApply_OffersEmpty: {
    fr: "Aucune offre publiée pour le moment. Après admission, vous pourrez être rattaché aux offres du groupe.",
    en: "No published offers yet. After admission you may be assigned to the group's offers.",
    es: "Aún no hay ofertas publicadas. Tras la admisión podrá ser asignado a las ofertas del grupo.",
    de: "Noch keine veröffentlichten Angebote. Nach der Aufnahme können Sie Gruppenangeboten zugeordnet werden.",
    pt: "Ainda não há ofertas publicadas. Após a admissão poderá ser associado às ofertas do grupo.",
    zh: "暂无已发布课程。入组后您可被分配到本组课程。",
    ar: "لا عروض منشورة بعد. بعد القبول يمكن ربطك بعروض المجموعة."
  },
  TeacherApply_ConditionsIntro: {
    fr: "Avant de créer votre compte enseignant et de soumettre votre candidature, lisez et acceptez les conditions ci-dessous.",
    en: "Before creating your teacher account and submitting your application, read and accept the terms below.",
    es: "Antes de crear su cuenta docente y enviar su candidatura, lea y acepte las condiciones.",
    de: "Bevor Sie Ihr Lehrkonto anlegen und sich bewerben, lesen und akzeptieren Sie die folgenden Bedingungen.",
    pt: "Antes de criar a conta de professor e submeter a candidatura, leia e aceite as condições.",
    zh: "在创建教师账户并提交申请前，请阅读并接受以下条款。",
    ar: "قبل إنشاء حساب المعلم وتقديم ترشيحك، اقرأ واقبل الشروط أدناه."
  },
  TeacherApply_AdmissionTitle: {
    fr: "Conditions de candidature au groupe",
    en: "Group application rules",
    es: "Condiciones de candidatura al grupo",
    de: "Bewerbungsbedingungen der Gruppe",
    pt: "Condições de candidatura ao grupo",
    zh: "向小组申请的条件",
    ar: "شروط الترشيح للمجموعة"
  },
  TeacherApply_Admission1: {
    fr: "Vous avez reçu une invitation nominative du groupe demandeur — ce n'est pas une inscription ouverte.",
    en: "You received a named invitation from the requesting group — this is not an open signup.",
    es: "Recibió una invitación nominativa del grupo solicitante: no es un registro abierto.",
    de: "Sie haben eine namentliche Einladung der anfragenden Gruppe erhalten — keine offene Anmeldung.",
    pt: "Recebeu um convite nominativo do grupo requerente — não é um registo aberto.",
    zh: "您收到了申请小组的具名邀请，这不是公开注册。",
    ar: "تلقيت دعوة اسمية من المجموعة الطالبة — هذا ليس تسجيلاً مفتوحًا."
  },
  TeacherApply_Admission2: {
    fr: "Vous créez un compte enseignant TutorSphere avec l'e-mail invité, puis vous soumettez votre dossier.",
    en: "You create a TutorSphere teacher account with the invited email, then submit your file.",
    es: "Crea una cuenta docente TutorSphere con el correo invitado y envía su expediente.",
    de: "Sie legen ein TutorSphere-Lehrkonto mit der eingeladenen E-Mail an und reichen Ihre Unterlagen ein.",
    pt: "Cria uma conta de professor TutorSphere com o e-mail convidado e submete o dossiê.",
    zh: "使用受邀邮箱创建教师账户，然后提交材料。",
    ar: "تنشئ حساب معلم TutorSphere بالبريد المدعو ثم تقدّم ملفك."
  },
  TeacherApply_Admission3: {
    fr: "Le groupe examine votre candidature. L'accès aux cours n'est ouvert qu'après approbation.",
    en: "The group reviews your application. Access to lessons opens only after approval.",
    es: "El grupo examina su candidatura. El acceso a las clases se abre solo tras la aprobación.",
    de: "Die Gruppe prüft Ihre Bewerbung. Unterrichtszugang erst nach Freigabe.",
    pt: "O grupo analisa a candidatura. O acesso às aulas abre só após aprovação.",
    zh: "小组审核您的申请。仅在获批后才能授课。",
    ar: "تراجع المجموعة ترشيحك. يُفتح الوصول إلى الدروس بعد الموافقة فقط."
  },
  TeacherApply_Admission4: {
    fr: "L'invitation expire à la date indiquée. Confirmez votre e-mail pour activer le compte.",
    en: "The invitation expires on the date shown. Confirm your email to activate the account.",
    es: "La invitación caduca en la fecha indicada. Confirme su correo para activar la cuenta.",
    de: "Die Einladung läuft am angegebenen Datum ab. Bestätigen Sie Ihre E-Mail, um das Konto zu aktivieren.",
    pt: "O convite expira na data indicada. Confirme o e-mail para ativar a conta.",
    zh: "邀请在所示日期到期。请确认邮箱以激活账户。",
    ar: "تنتهي الدعوة في التاريخ المبيّن. أكّد بريدك لتفعيل الحساب."
  },
  TeacherApply_AcceptAdmission: {
    fr: "J'ai lu et j'accepte les conditions de candidature : invitation nominative, examen par le groupe, cours après approbation.",
    en: "I have read and I accept the application rules: named invitation, group review, teaching after approval.",
    es: "He leído y acepto las condiciones: invitación nominativa, revisión del grupo, clases tras aprobación.",
    de: "Ich habe die Bewerbungsbedingungen gelesen und akzeptiere sie: namentliche Einladung, Gruppenprüfung, Unterricht nach Freigabe.",
    pt: "Li e aceito as condições: convite nominativo, análise do grupo, aulas após aprovação.",
    zh: "我已阅读并接受申请条件：具名邀请、小组审核、获批后授课。",
    ar: "قرأت وأقبل شروط الترشيح: دعوة اسمية، مراجعة المجموعة، التدريس بعد الموافقة."
  },
  TeacherApply_RegisterTitle: {
    fr: "S'enregistrer et soumettre",
    en: "Register and submit",
    es: "Registrarse y enviar",
    de: "Registrieren und einreichen",
    pt: "Registar e submeter",
    zh: "注册并提交",
    ar: "التسجيل والتقديم"
  },
  TeacherApply_RegisterLead: {
    fr: "Créez votre compte enseignant, acceptez les conditions, puis soumettez votre candidature au groupe.",
    en: "Create your teacher account, accept the terms, then submit your application to the group.",
    es: "Cree su cuenta docente, acepte las condiciones y envíe su candidatura al grupo.",
    de: "Legen Sie Ihr Lehrkonto an, akzeptieren Sie die Bedingungen und reichen Sie Ihre Bewerbung ein.",
    pt: "Crie a conta de professor, aceite as condições e submeta a candidatura ao grupo.",
    zh: "创建教师账户、接受条款，然后向小组提交申请。",
    ar: "أنشئ حساب المعلم، اقبل الشروط، ثم قدّم ترشيحك إلى المجموعة."
  },
  TeacherApply_EmailLocked: {
    fr: "Compte lié à cette invitation — l'adresse ne peut pas être modifiée.",
    en: "Account tied to this invitation — the address cannot be changed.",
    es: "Cuenta vinculada a esta invitación: la dirección no se puede modificar.",
    de: "Konto an diese Einladung gebunden — die Adresse kann nicht geändert werden.",
    pt: "Conta ligada a este convite — o endereço não pode ser alterado.",
    zh: "账户与此邀请绑定，邮箱不可更改。",
    ar: "الحساب مرتبط بهذه الدعوة — لا يمكن تغيير العنوان."
  },
  TeacherApply_Submit: {
    fr: "Soumettre ma candidature",
    en: "Submit my application",
    es: "Enviar mi candidatura",
    de: "Bewerbung einreichen",
    pt: "Submeter a minha candidatura",
    zh: "提交申请",
    ar: "تقديم ترشيحي"
  },
  TeacherApply_Success: {
    fr: "Candidature envoyée",
    en: "Application sent",
    es: "Candidatura enviada",
    de: "Bewerbung gesendet",
    pt: "Candidatura enviada",
    zh: "申请已发送",
    ar: "تم إرسال الترشيح"
  },
  TeacherApply_SuccessWait: {
    fr: "Confirmez votre e-mail, puis attendez l'examen du groupe. Vous serez prévenu lorsque votre dossier sera traité.",
    en: "Confirm your email, then wait for the group's review. You will be notified when your file is processed.",
    es: "Confirme su correo y espere la revisión del grupo. Recibirá un aviso cuando se trate su expediente.",
    de: "Bestätigen Sie Ihre E-Mail und warten Sie auf die Gruppenprüfung. Sie werden benachrichtigt, sobald Ihr Dossier bearbeitet wird.",
    pt: "Confirme o e-mail e aguarde a análise do grupo. Será avisado quando o dossiê for tratado.",
    zh: "请先确认邮箱，然后等待小组审核。处理完成后我们会通知您。",
    ar: "أكّد بريدك ثم انتظر مراجعة المجموعة. سنُعلمك عند معالجة ملفك."
  },
  TeacherApply_GoLogin: {
    fr: "Aller à la connexion enseignant",
    en: "Go to teacher sign-in",
    es: "Ir al inicio de sesión docente",
    de: "Zur Lehrer-Anmeldung",
    pt: "Ir para o início de sessão de professor",
    zh: "前往教师登录",
    ar: "الذهاب لتسجيل دخول المعلم"
  },
  TeacherApply_MustAccept: {
    fr: "Vous devez accepter les conditions de candidature, le code de conduite et la confidentialité.",
    en: "You must accept the application rules, the code of conduct and the privacy policy.",
    es: "Debe aceptar las condiciones de candidatura, el código de conducta y la privacidad.",
    de: "Sie müssen Bewerbungsbedingungen, Verhaltenskodex und Datenschutz akzeptieren.",
    pt: "Deve aceitar as condições de candidatura, o código de conduta e a privacidade.",
    zh: "您必须接受申请条件、行为准则和隐私政策。",
    ar: "يجب قبول شروط الترشيح ومدونة السلوك والخصوصية."
  }
};

function pick(v, file) {
  if (file.includes(".en.")) return v.en;
  if (file.includes(".es.")) return v.es;
  if (file.includes(".de.")) return v.de;
  if (file.includes(".pt.")) return v.pt;
  if (file.includes("zh-Hans")) return v.zh;
  if (file.includes(".ar.")) return v.ar;
  return v.fr;
}
function enc(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

const files = [
  "SharedResources.resx", "SharedResources.fr.resx", "SharedResources.en.resx",
  "SharedResources.es.resx", "SharedResources.de.resx", "SharedResources.pt.resx",
  "SharedResources.zh-Hans.resx", "SharedResources.ar.resx"
];

for (const file of files) {
  const p = path.join(__dirname, file);
  let xml = fs.readFileSync(p, "utf8");
  let added = 0;
  const blocks = [];
  for (const [k, v] of Object.entries(KEYS)) {
    if (xml.includes(`name="${k}"`)) continue;
    blocks.push(`  <data name="${k}" xml:space="preserve">\n    <value>${enc(pick(v, file))}</value>\n  </data>`);
    added++;
  }
  if (blocks.length)
    xml = xml.replace(/\s*<\/root>\s*$/, `\n${blocks.join("\n")}\n</root>\n`);
  fs.writeFileSync(p, xml, "utf8");
  console.log(file, "added", added);
}
