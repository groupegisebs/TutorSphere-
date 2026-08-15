import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const KEYS = {
  ExpertJoin_Title: {
    fr: "Invitation Expert",
    en: "Expert invitation",
    es: "Invitación de experto",
    de: "Experten-Einladung",
    pt: "Convite de especialista",
    zh: "专家邀请",
    ar: "دعوة خبير"
  },
  ExpertJoin_Badge: {
    fr: "Espace Expert",
    en: "Expert space",
    es: "Espacio experto",
    de: "Expertenbereich",
    pt: "Espaço especialista",
    zh: "专家空间",
    ar: "مساحة الخبير"
  },
  ExpertJoin_InvitesYou: {
    fr: "vous invite à rejoindre",
    en: "invites you to join",
    es: "le invita a unirse a",
    de: "lädt Sie ein, beizutreten:",
    pt: "convida-o a juntar-se a",
    zh: "邀请您加入",
    ar: "يدعوك للانضمام إلى"
  },
  ExpertJoin_Expires: {
    fr: "Valable jusqu'au {0}",
    en: "Valid until {0}",
    es: "Válida hasta el {0}",
    de: "Gültig bis {0}",
    pt: "Válido até {0}",
    zh: "有效期至 {0}",
    ar: "صالحة حتى {0}"
  },
  ExpertJoin_PlatformTitle: {
    fr: "TutorSphere, en bref",
    en: "TutorSphere at a glance",
    es: "TutorSphere en breve",
    de: "TutorSphere kurz erklärt",
    pt: "TutorSphere em resumo",
    zh: "TutorSphere 简介",
    ar: "TutorSphere باختصار"
  },
  ExpertJoin_PlatformLead: {
    fr: "TutorSphere relie familles, enseignants et groupes d'experts. Les Experts garantissent la qualité pédagogique des profils enseignants et des offres de leur groupe.",
    en: "TutorSphere connects families, teachers and expert groups. Experts guarantee the educational quality of teacher profiles and of their group's offers.",
    es: "TutorSphere conecta familias, docentes y grupos de expertos. Los expertos garantizan la calidad pedagógica de los perfiles docentes y de las ofertas de su grupo.",
    de: "TutorSphere verbindet Familien, Lehrkräfte und Expertengruppen. Experten sichern die pädagogische Qualität der Lehrerprofile und der Angebote ihrer Gruppe.",
    pt: "O TutorSphere liga famílias, professores e grupos de especialistas. Os especialistas garantem a qualidade pedagógica dos perfis docentes e das ofertas do seu grupo.",
    zh: "TutorSphere 连接家庭、教师与专家组。专家负责保证教师档案及本组课程的教学质量。",
    ar: "تربط TutorSphere الأسر والمعلمين ومجموعات الخبراء. يضمن الخبراء الجودة التربوية لملفات المعلمين وعروض مجموعتهم."
  },
  ExpertJoin_PlatformBullet1: {
    fr: "Revue collégiale des dossiers enseignants (éthique, sécurité des mineurs, qualité).",
    en: "Collegial review of teacher files (ethics, minor safety, quality).",
    es: "Revisión colegiada de los expedientes docentes (ética, seguridad de menores, calidad).",
    de: "Kollegiale Prüfung der Lehrerakten (Ethik, Minderjährigenschutz, Qualität).",
    pt: "Revisão colegial dos dossiês de professores (ética, segurança de menores, qualidade).",
    zh: "对教师档案进行同行评议（伦理、未成年人安全、质量）。",
    ar: "مراجعة جماعية لملفات المعلمين (الأخلاق، سلامة القُصَّر، الجودة)."
  },
  ExpertJoin_PlatformBullet2: {
    fr: "Conception et publication d'offres pédagogiques communes au groupe.",
    en: "Design and publication of shared group learning offers.",
    es: "Diseño y publicación de ofertas pedagógicas comunes al grupo.",
    de: "Erstellung und Veröffentlichung gemeinsamer Lernangebote der Gruppe.",
    pt: "Conceção e publicação de ofertas pedagógicas comuns ao grupo.",
    zh: "设计并发布本组共享的教学课程。",
    ar: "تصميم ونشر عروض تعليمية مشتركة للمجموعة."
  },
  ExpertJoin_PlatformBullet3: {
    fr: "Admission des nouveaux Experts par invitation, puis vote des membres (75 %).",
    en: "New Experts are admitted by invitation, then a member vote (75%).",
    es: "Los nuevos expertos se admiten por invitación y luego por voto de los miembros (75 %).",
    de: "Neue Experten werden per Einladung aufgenommen, danach Mitgliederabstimmung (75 %).",
    pt: "Novos especialistas são admitidos por convite e depois por voto dos membros (75 %).",
    zh: "新专家须经邀请，再由成员投票通过（75%）。",
    ar: "يُقبل الخبراء الجدد بدعوة ثم بتصويت الأعضاء (75٪)."
  },
  ExpertJoin_GroupTitle: {
    fr: "Profil du groupe",
    en: "Group profile",
    es: "Perfil del grupo",
    de: "Gruppenprofil",
    pt: "Perfil do grupo",
    zh: "小组简介",
    ar: "ملف المجموعة"
  },
  ExpertJoin_GroupDescriptionEmpty: {
    fr: "Ce groupe d'experts valide les enseignants et pilote les offres de son territoire.",
    en: "This expert group reviews teachers and steers the offers for its territory.",
    es: "Este grupo de expertos valida a los docentes y dirige las ofertas de su territorio.",
    de: "Diese Expertengruppe prüft Lehrkräfte und steuert die Angebote ihres Gebiets.",
    pt: "Este grupo de especialistas valida professores e conduz as ofertas do seu território.",
    zh: "该专家组负责审核教师并管理本地区课程。",
    ar: "تدقّق مجموعة الخبراء هذه في المعلمين وتدير عروض منطقتها."
  },
  ExpertJoin_International: {
    fr: "Groupe international",
    en: "International group",
    es: "Grupo internacional",
    de: "Internationale Gruppe",
    pt: "Grupo internacional",
    zh: "国际组",
    ar: "مجموعة دولية"
  },
  ExpertJoin_Members: {
    fr: "{0} membre(s)",
    en: "{0} member(s)",
    es: "{0} miembro(s)",
    de: "{0} Mitglied(er)",
    pt: "{0} membro(s)",
    zh: "{0} 位成员",
    ar: "{0} عضوًا"
  },
  ExpertJoin_Responsable: {
    fr: "Responsable",
    en: "Group manager",
    es: "Responsable",
    de: "Gruppenleiter",
    pt: "Responsável",
    zh: "负责人",
    ar: "المسؤول"
  },
  ExpertJoin_OffersTitle: {
    fr: "Offres du groupe",
    en: "Group offers",
    es: "Ofertas del grupo",
    de: "Gruppenangebote",
    pt: "Ofertas do grupo",
    zh: "小组课程",
    ar: "عروض المجموعة"
  },
  ExpertJoin_OffersEmpty: {
    fr: "Aucune offre publiée pour le moment. En tant qu'Expert, vous pourrez contribuer à en créer.",
    en: "No published offers yet. As an Expert, you will be able to help create them.",
    es: "Aún no hay ofertas publicadas. Como experto, podrá ayudar a crearlas.",
    de: "Noch keine veröffentlichten Angebote. Als Experte können Sie welche mitgestalten.",
    pt: "Ainda não há ofertas publicadas. Como especialista, poderá ajudar a criá-las.",
    zh: "暂无已发布课程。成为专家后，您可以参与创建。",
    ar: "لا عروض منشورة بعد. كخبير ستتمكن من المساهمة في إنشائها."
  },
  ExpertJoin_OfferInternational: {
    fr: "Offre internationale",
    en: "International offer",
    es: "Oferta internacional",
    de: "Internationales Angebot",
    pt: "Oferta internacional",
    zh: "国际课程",
    ar: "عرض دولي"
  },
  ExpertJoin_ConditionsTitle: {
    fr: "Conditions à accepter",
    en: "Terms you must accept",
    es: "Condiciones que debe aceptar",
    de: "Bedingungen, die Sie akzeptieren müssen",
    pt: "Condições a aceitar",
    zh: "须接受的条款",
    ar: "الشروط الواجب قبولها"
  },
  ExpertJoin_ConditionsIntro: {
    fr: "Avant de créer votre compte Expert et de soumettre votre candidature, lisez et acceptez les trois ensembles ci-dessous. Ils s'appliquent dès l'inscription.",
    en: "Before creating your Expert account and submitting your application, read and accept the three sets below. They apply as soon as you register.",
    es: "Antes de crear su cuenta de experto y enviar su candidatura, lea y acepte los tres conjuntos siguientes. Se aplican desde el registro.",
    de: "Bevor Sie Ihr Expertenkonto anlegen und sich bewerben, lesen und akzeptieren Sie die drei folgenden Regelwerke. Sie gelten ab der Registrierung.",
    pt: "Antes de criar a sua conta de especialista e submeter a candidatura, leia e aceite os três conjuntos abaixo. Aplicam-se desde o registo.",
    zh: "在创建专家账户并提交申请前，请阅读并接受以下三套条款。它们自注册起生效。",
    ar: "قبل إنشاء حساب الخبير وتقديم ترشيحك، اقرأ واقبل المجموعات الثلاث أدناه. تسري منذ التسجيل."
  },
  ExpertJoin_AdmissionTitle: {
    fr: "Conditions d'admission au groupe",
    en: "Group admission rules",
    es: "Condiciones de admisión al grupo",
    de: "Aufnahmebedingungen der Gruppe",
    pt: "Condições de admissão ao grupo",
    zh: "入组条件",
    ar: "شروط القبول في المجموعة"
  },
  ExpertJoin_AdmissionIntro: {
    fr: "Cette page n'est pas une inscription ouverte : vous avez reçu une invitation nominative du Responsable.",
    en: "This page is not an open signup: you received a named invitation from the group manager.",
    es: "Esta página no es un registro abierto: recibió una invitación nominativa del responsable.",
    de: "Dies ist keine offene Anmeldung: Sie haben eine namentliche Einladung des Gruppenleiters erhalten.",
    pt: "Esta página não é um registo aberto: recebeu um convite nominativo do responsável.",
    zh: "本页不是公开注册：您收到了负责人的具名邀请。",
    ar: "هذه الصفحة ليست تسجيلاً مفتوحًا: تلقيت دعوة اسمية من المسؤول."
  },
  ExpertJoin_Admission1: {
    fr: "Vous créez (ou confirmez) un compte Expert TutorSphere lié à l'adresse e-mail invitée.",
    en: "You create (or confirm) a TutorSphere Expert account linked to the invited email address.",
    es: "Crea (o confirma) una cuenta de experto TutorSphere vinculada al correo invitado.",
    de: "Sie erstellen (oder bestätigen) ein TutorSphere-Expertenkonto zur eingeladenen E-Mail-Adresse.",
    pt: "Cria (ou confirma) uma conta de especialista TutorSphere associada ao e-mail convidado.",
    zh: "您将创建（或确认）绑定受邀邮箱的 TutorSphere 专家账户。",
    ar: "تنشئ (أو تؤكد) حساب خبير TutorSphere مرتبطًا بالبريد المدعو."
  },
  ExpertJoin_Admission2: {
    fr: "Après soumission, les membres actifs votent. L'admission exige l'accord d'au moins 75 % des votants éligibles.",
    en: "After you submit, active members vote. Admission requires at least 75% of eligible voters.",
    es: "Tras el envío, los miembros activos votan. La admisión exige al menos el 75 % de los votantes elegibles.",
    de: "Nach der Einreichung stimmen die aktiven Mitglieder ab. Die Aufnahme erfordert mindestens 75 % der stimmberechtigten Mitglieder.",
    pt: "Após a submissão, os membros ativos votam. A admissão exige pelo menos 75 % dos votantes elegíveis.",
    zh: "提交后，活跃成员将投票。入组需获得至少 75% 合格票。",
    ar: "بعد التقديم، يصوّت الأعضاء النشطون. يتطلب القبول موافقة 75٪ على الأقل من المصوّتين المؤهلين."
  },
  ExpertJoin_Admission3: {
    fr: "Un Expert n'appartient qu'à un seul groupe à la fois. Le Responsable n'est pas votre employeur ; vous restez indépendant.",
    en: "An Expert belongs to only one group at a time. The group manager is not your employer; you remain independent.",
    es: "Un experto pertenece a un solo grupo a la vez. El responsable no es su empleador; permanece independiente.",
    de: "Ein Experte gehört jeweils nur einer Gruppe an. Der Gruppenleiter ist nicht Ihr Arbeitgeber; Sie bleiben unabhängig.",
    pt: "Um especialista pertence a um só grupo de cada vez. O responsável não é o seu empregador; permanece independente.",
    zh: "每位专家同一时间只能属于一个组。负责人不是您的雇主；您保持独立。",
    ar: "ينتمي الخبير إلى مجموعة واحدة فقط في آن واحد. المسؤول ليس صاحب عملك؛ تبقى مستقلاً."
  },
  ExpertJoin_Admission4: {
    fr: "L'invitation expire à la date indiquée. Un petit groupe peut exiger une validation supplémentaire de la plateforme.",
    en: "The invitation expires on the date shown. A small group may also require platform validation.",
    es: "La invitación caduca en la fecha indicada. Un grupo pequeño puede exigir una validación adicional de la plataforma.",
    de: "Die Einladung läuft am angegebenen Datum ab. Eine kleine Gruppe kann zusätzlich eine Plattformprüfung verlangen.",
    pt: "O convite expira na data indicada. Um grupo pequeno pode exigir validação extra da plataforma.",
    zh: "邀请在所示日期到期。小组可能还需平台额外审核。",
    ar: "تنتهي الدعوة في التاريخ المبيّن. قد تتطلب مجموعة صغيرة تحققًا إضافيًا من المنصة."
  },
  ExpertJoin_AcceptAdmission: {
    fr: "J'ai lu et j'accepte les conditions d'admission au groupe (invitation, vote des membres, un seul groupe, indépendance).",
    en: "I have read and I accept the group admission rules (invitation, member vote, one group, independence).",
    es: "He leído y acepto las condiciones de admisión al grupo (invitación, voto de los miembros, un solo grupo, independencia).",
    de: "Ich habe die Aufnahmebedingungen gelesen und akzeptiere sie (Einladung, Mitgliederabstimmung, eine Gruppe, Unabhängigkeit).",
    pt: "Li e aceito as condições de admissão ao grupo (convite, voto dos membros, um só grupo, independência).",
    zh: "我已阅读并接受入组条件（邀请、成员投票、仅一组、保持独立）。",
    ar: "لقد قرأت وأقبل شروط القبول في المجموعة (الدعوة، تصويت الأعضاء، مجموعة واحدة، الاستقلال)."
  },
  ExpertJoin_ReadConduct: {
    fr: "Lire le code de conduite Expert complet",
    en: "Read the full Expert code of conduct",
    es: "Leer el código de conducta de experto completo",
    de: "Vollständigen Experten-Verhaltenskodex lesen",
    pt: "Ler o código de conduta de especialista completo",
    zh: "阅读完整专家行为准则",
    ar: "قراءة مدونة سلوك الخبير كاملة"
  },
  ExpertJoin_ReadPrivacy: {
    fr: "Lire la politique de confidentialité complète",
    en: "Read the full privacy policy",
    es: "Leer la política de privacidad completa",
    de: "Vollständige Datenschutzrichtlinie lesen",
    pt: "Ler a política de privacidade completa",
    zh: "阅读完整隐私政策",
    ar: "قراءة سياسة الخصوصية كاملة"
  },
  ExpertJoin_RegisterTitle: {
    fr: "S'enregistrer comme Expert",
    en: "Register as an Expert",
    es: "Registrarse como experto",
    de: "Als Experte registrieren",
    pt: "Registar-se como especialista",
    zh: "注册为专家",
    ar: "التسجيل كخبير"
  },
  ExpertJoin_RegisterLead: {
    fr: "Complétez votre profil, acceptez les conditions, puis soumettez votre candidature au groupe.",
    en: "Complete your profile, accept the terms, then submit your application to the group.",
    es: "Complete su perfil, acepte las condiciones y envíe su candidatura al grupo.",
    de: "Vervollständigen Sie Ihr Profil, akzeptieren Sie die Bedingungen und reichen Sie Ihre Bewerbung ein.",
    pt: "Complete o seu perfil, aceite as condições e submeta a candidatura ao grupo.",
    zh: "完善资料、接受条款，然后向小组提交申请。",
    ar: "أكمل ملفك، اقبل الشروط، ثم قدّم ترشيحك إلى المجموعة."
  },
  ExpertJoin_EmailLocked: {
    fr: "Compte lié à cette invitation — l'adresse ne peut pas être modifiée.",
    en: "Account tied to this invitation — the address cannot be changed.",
    es: "Cuenta vinculada a esta invitación: la dirección no se puede modificar.",
    de: "Konto an diese Einladung gebunden — die Adresse kann nicht geändert werden.",
    pt: "Conta ligada a este convite — o endereço não pode ser alterado.",
    zh: "账户与此邀请绑定，邮箱不可更改。",
    ar: "الحساب مرتبط بهذه الدعوة — لا يمكن تغيير العنوان."
  },
  ExpertJoin_Password: {
    fr: "Mot de passe du compte Expert",
    en: "Expert account password",
    es: "Contraseña de la cuenta de experto",
    de: "Passwort des Expertenkontos",
    pt: "Palavra-passe da conta de especialista",
    zh: "专家账户密码",
    ar: "كلمة مرور حساب الخبير"
  },
  ExpertJoin_PasswordHint: {
    fr: "8 caractères minimum, un chiffre et un caractère spécial.",
    en: "At least 8 characters, one digit and one special character.",
    es: "Mínimo 8 caracteres, un número y un carácter especial.",
    de: "Mindestens 8 Zeichen, eine Ziffer und ein Sonderzeichen.",
    pt: "Mínimo de 8 caracteres, um dígito e um carácter especial.",
    zh: "至少 8 个字符，须含数字和特殊符号。",
    ar: "8 أحرف على الأقل، رقم واحد وحرف خاص."
  },
  ExpertJoin_Specialty: {
    fr: "Spécialité",
    en: "Specialty",
    es: "Especialidad",
    de: "Fachgebiet",
    pt: "Especialidade",
    zh: "专长",
    ar: "التخصص"
  },
  ExpertJoin_Presentation: {
    fr: "Présentation",
    en: "Introduction",
    es: "Presentación",
    de: "Vorstellung",
    pt: "Apresentação",
    zh: "自我介绍",
    ar: "نبذة تعريفية"
  },
  ExpertJoin_Submit: {
    fr: "Soumettre ma candidature",
    en: "Submit my application",
    es: "Enviar mi candidatura",
    de: "Bewerbung einreichen",
    pt: "Submeter a minha candidatura",
    zh: "提交申请",
    ar: "تقديم ترشيحي"
  },
  ExpertJoin_Submitting: {
    fr: "Envoi…",
    en: "Sending…",
    es: "Enviando…",
    de: "Senden…",
    pt: "A enviar…",
    zh: "正在提交…",
    ar: "جارٍ الإرسال…"
  },
  ExpertJoin_SubmitFail: {
    fr: "Échec de la soumission.",
    en: "Submission failed.",
    es: "Error al enviar.",
    de: "Übermittlung fehlgeschlagen.",
    pt: "Falha na submissão.",
    zh: "提交失败。",
    ar: "فشل التقديم."
  },
  ExpertJoin_Decline: {
    fr: "Refuser l'invitation",
    en: "Decline the invitation",
    es: "Rechazar la invitación",
    de: "Einladung ablehnen",
    pt: "Recusar o convite",
    zh: "拒绝邀请",
    ar: "رفض الدعوة"
  },
  ExpertJoin_Success: {
    fr: "Candidature soumise",
    en: "Application submitted",
    es: "Candidatura enviada",
    de: "Bewerbung eingereicht",
    pt: "Candidatura submetida",
    zh: "申请已提交",
    ar: "تم تقديم الترشيح"
  },
  ExpertJoin_SuccessWait: {
    fr: "Votre compte Expert est créé. L'accès au groupe s'ouvrira après le vote des membres (75 %) ou, le cas échéant, la validation de la plateforme. Vous serez prévenu par e-mail.",
    en: "Your Expert account is created. Group access opens after the member vote (75%) or, if needed, platform validation. You will be notified by email.",
    es: "Su cuenta de experto está creada. El acceso al grupo se abrirá tras el voto (75 %) o, si procede, la validación de la plataforma. Recibirá un correo.",
    de: "Ihr Expertenkonto ist erstellt. Der Gruppenzugang öffnet sich nach der Abstimmung (75 %) oder ggf. der Plattformprüfung. Sie werden per E-Mail informiert.",
    pt: "A sua conta de especialista foi criada. O acesso ao grupo abre após o voto (75 %) ou, se necessário, a validação da plataforma. Será avisado por e-mail.",
    zh: "专家账户已创建。入组权限将在成员投票（75%）或平台审核后开通。我们将通过邮件通知您。",
    ar: "تم إنشاء حساب الخبير. يُفتح الوصول إلى المجموعة بعد تصويت الأعضاء (75٪) أو تحقق المنصة إن لزم. سنُعلمك بالبريد."
  },
  ExpertJoin_GoLogin: {
    fr: "Aller à la connexion Expert",
    en: "Go to Expert sign-in",
    es: "Ir al inicio de sesión experto",
    de: "Zur Experten-Anmeldung",
    pt: "Ir para o início de sessão de especialista",
    zh: "前往专家登录",
    ar: "الذهاب لتسجيل دخول الخبير"
  },
  ExpertJoin_Back: {
    fr: "Retour à l'accueil",
    en: "Back to home",
    es: "Volver al inicio",
    de: "Zurück zur Startseite",
    pt: "Voltar ao início",
    zh: "返回首页",
    ar: "العودة إلى الصفحة الرئيسية"
  },
  ExpertJoin_InvalidLink: {
    fr: "Lien d'invitation invalide.",
    en: "Invalid invitation link.",
    es: "Enlace de invitación no válido.",
    de: "Ungültiger Einladungslink.",
    pt: "Ligação de convite inválida.",
    zh: "邀请链接无效。",
    ar: "رابط الدعوة غير صالح."
  },
  ExpertJoin_NotFound: {
    fr: "Invitation introuvable ou expirée.",
    en: "Invitation not found or expired.",
    es: "Invitación no encontrada o caducada.",
    de: "Einladung nicht gefunden oder abgelaufen.",
    pt: "Convite não encontrado ou expirado.",
    zh: "未找到邀请或已过期。",
    ar: "الدعوة غير موجودة أو منتهية."
  },
  ExpertJoin_MustAccept: {
    fr: "Vous devez accepter les trois conditions (admission, code de conduite, confidentialité).",
    en: "You must accept all three terms (admission, code of conduct, privacy).",
    es: "Debe aceptar las tres condiciones (admisión, código de conducta, privacidad).",
    de: "Sie müssen alle drei Bedingungen akzeptieren (Aufnahme, Verhaltenskodex, Datenschutz).",
    pt: "Deve aceitar as três condições (admissão, código de conduta, privacidade).",
    zh: "您必须接受全部三项条件（入组、行为准则、隐私）。",
    ar: "يجب قبول الشروط الثلاثة (القبول، مدونة السلوك، الخصوصية)."
  },
  ExpertJoin_MustPassword: {
    fr: "Choisissez un mot de passe pour votre compte Expert.",
    en: "Choose a password for your Expert account.",
    es: "Elija una contraseña para su cuenta de experto.",
    de: "Wählen Sie ein Passwort für Ihr Expertenkonto.",
    pt: "Escolha uma palavra-passe para a sua conta de especialista.",
    zh: "请为专家账户设置密码。",
    ar: "اختر كلمة مرور لحساب الخبير."
  },
  ExpertJoin_ExistingAccount: {
    fr: "Un compte existe déjà pour cet e-mail : il sera utilisé pour cette candidature.",
    en: "An account already exists for this email: it will be used for this application.",
    es: "Ya existe una cuenta para este correo: se usará para esta candidatura.",
    de: "Für diese E-Mail existiert bereits ein Konto: es wird für diese Bewerbung verwendet.",
    pt: "Já existe uma conta para este e-mail: será usada nesta candidatura.",
    zh: "该邮箱已有账户，将用于本次申请。",
    ar: "يوجد حساب لهذا البريد بالفعل: سيُستخدم لهذه الترشيح."
  },
  ExpertConduct_Title: {
    fr: "Code de conduite des Experts TutorSphere",
    en: "TutorSphere Expert code of conduct",
    es: "Código de conducta de los expertos TutorSphere",
    de: "Verhaltenskodex der TutorSphere-Experten",
    pt: "Código de conduta dos especialistas TutorSphere",
    zh: "TutorSphere 专家行为准则",
    ar: "مدونة سلوك خبراء TutorSphere"
  },
  ExpertConduct_VersionLabel: {
    fr: "Version",
    en: "Version",
    es: "Versión",
    de: "Version",
    pt: "Versão",
    zh: "版本",
    ar: "الإصدار"
  },
  ExpertConduct_Intro: {
    fr: "En devenant Expert, vous acceptez d'agir avec indépendance, équité et confidentialité dans la revue des enseignants, le vote d'admission et le pilotage des offres de votre groupe.",
    en: "By becoming an Expert you agree to act with independence, fairness and confidentiality when reviewing teachers, voting on admissions and steering your group's offers.",
    es: "Al convertirse en experto, acepta actuar con independencia, equidad y confidencialidad en la revisión de docentes, el voto de admisión y el gobierno de las ofertas de su grupo.",
    de: "Als Experte handeln Sie unabhängig, fair und vertraulich bei der Prüfung von Lehrkräften, bei Aufnahmeabstimmungen und bei der Steuerung der Gruppenangebote.",
    pt: "Ao tornar-se especialista, aceita agir com independência, equidade e confidencialidade na revisão de professores, no voto de admissão e na gestão das ofertas do grupo.",
    zh: "成为专家即表示您同意在审核教师、入组投票及本组课程管理中保持独立、公平与保密。",
    ar: "بتحولك إلى خبير، توافق على التصرّف باستقلال وإنصاف وسرية عند مراجعة المعلمين والتصويت على القبول وإدارة عروض مجموعتك."
  },
  ExpertConduct_LegalNote: {
    fr: "Ce code ne remplace pas les lois applicables (protection des mineurs, données personnelles, droits de la personne). En cas de conflit, la loi prévaut. TutorSphere est une plateforme de mise en relation : vous n'êtes pas salarié de TutorSphere ni du groupe.",
    en: "This code does not replace applicable laws (child protection, personal data, human rights). If there is a conflict, the law prevails. TutorSphere is a matching platform: you are not an employee of TutorSphere or of the group.",
    es: "Este código no sustituye las leyes aplicables. En caso de conflicto, prima la ley. TutorSphere es una plataforma de puesta en relación: no es empleado de TutorSphere ni del grupo.",
    de: "Dieser Kodex ersetzt keine geltenden Gesetze. Bei Konflikten geht das Gesetz vor. TutorSphere ist eine Vermittlungsplattform: Sie sind weder Angestellter von TutorSphere noch der Gruppe.",
    pt: "Este código não substitui as leis aplicáveis. Em caso de conflito, a lei prevalece. O TutorSphere é uma plataforma de ligação: não é trabalhador do TutorSphere nem do grupo.",
    zh: "本准则不能取代适用法律。发生冲突时以法律为准。TutorSphere 是撮合平台：您既非 TutorSphere 雇员，也非该组雇员。",
    ar: "لا تحل هذه المدونة محل القوانين المعمول بها. عند التعارض تسود القانون. TutorSphere منصة وصل: لست موظفًا لدى TutorSphere ولا لدى المجموعة."
  },
  ExpertConduct_AcceptCheckbox: {
    fr: "J'ai lu et j'accepte le code de conduite des Experts TutorSphere (version en vigueur). Je m'engage à le respecter ; un manquement peut entraîner la suspension de mon accès.",
    en: "I have read and I accept the TutorSphere Expert code of conduct (current version). I will comply with it; a breach may lead to suspension of my access.",
    es: "He leído y acepto el código de conducta de los expertos TutorSphere (versión vigente). Me comprometo a respetarlo; un incumplimiento puede suspender mi acceso.",
    de: "Ich habe den geltenden TutorSphere-Expertenkodex gelesen und akzeptiere ihn. Ein Verstoß kann zur Sperrung meines Zugangs führen.",
    pt: "Li e aceito o código de conduta dos especialistas TutorSphere (versão em vigor). Um incumprimento pode suspender o meu acesso.",
    zh: "我已阅读并接受现行 TutorSphere 专家行为准则。违规可能导致暂停访问。",
    ar: "قرأت وأقبل مدونة سلوك خبراء TutorSphere (النسخة السارية). قد يؤدي الإخلال إلى تعليق وصولي."
  },
  ExpertConduct_Bullet_Integrity: {
    fr: "Intégrité : pas de favoritisme, de conflit d'intérêts non déclaré, ni de pression sur un vote.",
    en: "Integrity: no favouritism, undeclared conflicts of interest, or pressure on a vote.",
    es: "Integridad: sin favoritismo, conflictos de interés no declarados ni presión sobre un voto.",
    de: "Integrität: kein Vetternwirtschaft, keine unveröffentlichten Interessenkonflikte, kein Abstimmungsdruck.",
    pt: "Integridade: sem favoritismo, conflitos de interesse não declarados nem pressão sobre um voto.",
    zh: "诚信：不得徇私、隐瞒利益冲突或干预投票。",
    ar: "النزاهة: لا محاباة ولا تضارب مصالح غير معلن ولا ضغط على التصويت."
  },
  ExpertConduct_Bullet_Confidentiality: {
    fr: "Confidentialité des dossiers enseignants, des votes et des données des familles.",
    en: "Confidentiality of teacher files, votes and family data.",
    es: "Confidencialidad de los expedientes docentes, los votos y los datos de las familias.",
    de: "Vertraulichkeit der Lehrerakten, Abstimmungen und Familiendaten.",
    pt: "Confidencialidade dos dossiês de professores, votos e dados das famílias.",
    zh: "对教师档案、投票及家庭数据保密。",
    ar: "سرية ملفات المعلمين والأصوات وبيانات الأسر."
  },
  ExpertConduct_Bullet_Fairness: {
    fr: "Équité et non-discrimination dans toute revue, admission ou offre.",
    en: "Fairness and non-discrimination in every review, admission or offer.",
    es: "Equidad y no discriminación en toda revisión, admisión u oferta.",
    de: "Fairness und Nichtdiskriminierung bei jeder Prüfung, Aufnahme oder jedem Angebot.",
    pt: "Equidade e não discriminação em qualquer revisão, admissão ou oferta.",
    zh: "在任何审核、录取或课程中保持公平、不歧视。",
    ar: "الإنصاف وعدم التمييز في كل مراجعة أو قبول أو عرض."
  },
  ExpertConduct_Bullet_Quality: {
    fr: "Qualité pédagogique des offres du groupe : clarté, niveaux adaptés, prix honnêtes.",
    en: "Educational quality of group offers: clarity, suitable levels, honest pricing.",
    es: "Calidad pedagógica de las ofertas del grupo: claridad, niveles adecuados, precios honestos.",
    de: "Pädagogische Qualität der Gruppenangebote: Klarheit, passende Niveaus, ehrliche Preise.",
    pt: "Qualidade pedagógica das ofertas do grupo: clareza, níveis adequados, preços honestos.",
    zh: "本组课程的教学质量：表述清晰、程度合适、定价诚实。",
    ar: "الجودة التربوية لعروض المجموعة: وضوح، مستويات مناسبة، أسعار نزيهة."
  },
  ExpertConduct_Bullet_Vote: {
    fr: "Vote de bonne foi, dans les délais, sans déléguer son suffrage.",
    en: "Vote in good faith, on time, without delegating your ballot.",
    es: "Votar de buena fe, en plazo, sin delegar el sufragio.",
    de: "Stimmabgabe nach bestem Wissen, fristgerecht, ohne Stimme zu übertragen.",
    pt: "Votar de boa-fé, nos prazos, sem delegar o voto.",
    zh: "本着善意、按时投票，且不得转授选票。",
    ar: "التصويت بحسن نية وفي المواعيد دون تفويض الصوت."
  },
  ExpertConduct_Bullet_Law: {
    fr: "Respect des lois (protection des mineurs, données personnelles) et signalement de tout danger.",
    en: "Comply with the law (child protection, personal data) and report any danger.",
    es: "Respetar la ley (protección de menores, datos personales) y comunicar cualquier peligro.",
    de: "Einhaltung der Gesetze (Minderjährigenschutz, Datenschutz) und Meldung jeder Gefahr.",
    pt: "Respeitar a lei (proteção de menores, dados pessoais) e comunicar qualquer perigo.",
    zh: "遵守法律（未成年人保护、个人数据），并报告任何危险。",
    ar: "احترام القانون (حماية القُصَّر والبيانات الشخصية) والإبلاغ عن أي خطر."
  },
  ExpertConduct_S1_Title: { fr: "Objet et portée", en: "Purpose and scope", es: "Objeto y alcance", de: "Zweck und Geltungsbereich", pt: "Objeto e âmbito", zh: "目的与范围", ar: "الغرض والنطاق" },
  ExpertConduct_S1_Body: {
    fr: "Ce code s'applique à tout Expert, Responsable de groupe et candidat admis. Il vise la protection des élèves, des enseignants et de la plateforme. L'acceptation est une condition d'admission.",
    en: "This code applies to every Expert, group manager and admitted candidate. It protects students, teachers and the platform. Acceptance is a condition of admission.",
    es: "Este código se aplica a todo experto, responsable de grupo y candidato admitido. Protege a alumnos, docentes y la plataforma. La aceptación es condición de admisión.",
    de: "Dieser Kodex gilt für jeden Experten, Gruppenleiter und zugelassenen Bewerber. Er schützt Schüler, Lehrkräfte und die Plattform. Die Annahme ist Aufnahmevoraussetzung.",
    pt: "Este código aplica-se a todo especialista, responsável de grupo e candidato admitido. Protege alunos, professores e a plataforma. A aceitação é condição de admissão.",
    zh: "本准则适用于每位专家、组长及获准入选者，旨在保护学生、教师与平台。接受本准则是入组条件。",
    ar: "تسري هذه المدونة على كل خبير ومسؤول مجموعة ومرشح مقبول. تحمي التلاميذ والمعلمين والمنصة. القبول شرط للانضمام."
  },
  ExpertConduct_S2_Title: { fr: "Indépendance et conflits d'intérêts", en: "Independence and conflicts of interest", es: "Independencia y conflictos de interés", de: "Unabhängigkeit und Interessenkonflikte", pt: "Independência e conflitos de interesse", zh: "独立性与利益冲突", ar: "الاستقلال وتضارب المصالح" },
  ExpertConduct_S2_Body: {
    fr: "Vous examinez les dossiers sans favoritisme. Vous déclarez tout lien familial, amical, financier ou concurrentiel avec un enseignant, un candidat ou une offre. En cas de conflit, vous vous déportez du vote et de la revue.",
    en: "You review files without favouritism. You declare any family, personal, financial or competitive link with a teacher, a candidate or an offer. If there is a conflict, you recuse yourself from the vote and the review.",
    es: "Examina los expedientes sin favoritismo. Declara cualquier vínculo familiar, personal, financiero o competitivo. Si hay conflicto, se abstiene del voto y de la revisión.",
    de: "Sie prüfen Akten ohne Bevorzugung. Sie legen familiäre, persönliche, finanzielle oder wettbewerbliche Bindungen offen. Bei einem Konflikt enthalten Sie sich der Abstimmung und der Prüfung.",
    pt: "Analisa os dossiês sem favoritismo. Declara qualquer ligação familiar, pessoal, financeira ou concorrencial. Em caso de conflito, recusa-se do voto e da revisão.",
    zh: "审核须无私。与教师、候选人或课程存在亲属、私交、财务或竞争关系须申报；有冲突时回避投票与评审。",
    ar: "تراجع الملفات دون محاباة. تعلن أي صلة عائلية أو شخصية أو مالية أو تنافسية. عند التعارض تنسحب من التصويت والمراجعة."
  },
  ExpertConduct_S3_Title: { fr: "Confidentialité", en: "Confidentiality", es: "Confidencialidad", de: "Vertraulichkeit", pt: "Confidencialidade", zh: "保密", ar: "السرية" },
  ExpertConduct_S3_Body: {
    fr: "Les pièces d'un dossier enseignant, les délibérations et les votes restent internes au groupe. Vous ne les copiez, partagez ni publiez hors des outils TutorSphere, sauf obligation légale.",
    en: "Teacher-file exhibits, deliberations and votes stay inside the group. You do not copy, share or publish them outside TutorSphere tools, except where the law requires it.",
    es: "Las piezas de un expediente, las deliberaciones y los votos permanecen internos. No se copian ni publican fuera de TutorSphere, salvo obligación legal.",
    de: "Unterlagen, Beratungen und Stimmen bleiben gruppenintern. Sie werden außerhalb der TutorSphere-Werkzeuge nicht kopiert oder veröffentlicht, außer bei gesetzlicher Pflicht.",
    pt: "As peças de um dossiê, as deliberações e os votos permanecem internos. Não os copia nem publica fora do TutorSphere, salvo obrigação legal.",
    zh: "教师材料、评议与投票仅限组内。除法定义务外，不得复制或外传。",
    ar: "تبقى مستندات الملف والمداولات والأصوات داخل المجموعة. لا تُنسخ ولا تُنشر خارج أدوات TutorSphere إلا بموجب القانون."
  },
  ExpertConduct_S4_Title: { fr: "Équité et non-discrimination", en: "Fairness and non-discrimination", es: "Equidad y no discriminación", de: "Fairness und Nichtdiskriminierung", pt: "Equidade e não discriminação", zh: "公平与非歧视", ar: "الإنصاف وعدم التمييز" },
  ExpertConduct_S4_Body: {
    fr: "Les décisions s'appuient sur des critères pédagogiques et de sécurité, jamais sur l'origine, le genre, la religion, le handicap ou d'autres motifs protégés.",
    en: "Decisions rest on educational and safety criteria, never on origin, gender, religion, disability or other protected grounds.",
    es: "Las decisiones se basan en criterios pedagógicos y de seguridad, nunca en origen, género, religión, discapacidad u otros motivos protegidos.",
    de: "Entscheidungen beruhen auf pädagogischen und Sicherheitskriterien, nicht auf Herkunft, Geschlecht, Religion, Behinderung oder anderen geschützten Merkmalen.",
    pt: "As decisões assentam em critérios pedagógicos e de segurança, nunca na origem, género, religião, deficiência ou outros motivos protegidos.",
    zh: "决策仅基于教学与安全标准，不得基于出身、性别、宗教、残障或其他受保护事由。",
    ar: "تستند القرارات إلى معايير تربوية وأمنية، لا إلى الأصل أو الجنس أو الدين أو الإعاقة أو أي سبب محمي."
  },
  ExpertConduct_S5_Title: { fr: "Vote collégial", en: "Collegial vote", es: "Voto colegiado", de: "Kollegiale Abstimmung", pt: "Voto colegial", zh: "同行投票", ar: "التصويت الجماعي" },
  ExpertConduct_S5_Body: {
    fr: "Vous votez personnellement, de bonne foi et dans le délai. Vous ne vendez pas votre voix. Le seuil d'admission est de 75 % des membres éligibles, sauf validation plateforme pour un très petit groupe.",
    en: "You vote personally, in good faith and on time. You do not sell your vote. The admission threshold is 75% of eligible members, except platform validation for a very small group.",
    es: "Vota personalmente, de buena fe y en plazo. No vende su voto. El umbral de admisión es el 75 % de los miembros elegibles, salvo validación de plataforma para un grupo muy pequeño.",
    de: "Sie stimmen persönlich, redlich und fristgerecht ab. Stimmenkauf ist verboten. Die Aufnahmeschwelle beträgt 75 % der Stimmberechtigten, außer bei Plattformprüfung in sehr kleinen Gruppen.",
    pt: "Vota pessoalmente, de boa-fé e no prazo. Não vende o voto. O limiar de admissão é 75 % dos membros elegíveis, salvo validação da plataforma num grupo muito pequeno.",
    zh: "须亲自、善意、按时投票，禁止买卖选票。入组门槛为合格成员的 75%，极小团体可由平台核定。",
    ar: "تصوّت شخصيًا وبحسن نية وفي الأجل. لا تبيع صوتك. عتبة القبول 75٪ من الأعضاء المؤهلين، إلا عند تحقق المنصة لمجموعة صغيرة جدًا."
  },
  ExpertConduct_S6_Title: { fr: "Qualité des offres", en: "Quality of offers", es: "Calidad de las ofertas", de: "Qualität der Angebote", pt: "Qualidade das ofertas", zh: "课程质量", ar: "جودة العروض" },
  ExpertConduct_S6_Body: {
    fr: "Les offres du groupe doivent décrire clairement le public, les niveaux, la langue et le prix. Vous n'approuvez pas une offre trompeuse ou hors marché raisonnable.",
    en: "Group offers must clearly describe audience, levels, language and price. You do not approve a misleading offer or one far outside a reasonable market.",
    es: "Las ofertas del grupo deben describir claramente público, niveles, idioma y precio. No se aprueba una oferta engañosa o fuera de un mercado razonable.",
    de: "Gruppenangebote müssen Zielgruppe, Niveaus, Sprache und Preis klar beschreiben. Irreführende oder marktfremde Angebote werden nicht genehmigt.",
    pt: "As ofertas do grupo devem descrever claramente público, níveis, língua e preço. Não se aprova uma oferta enganosa ou fora de um mercado razoável.",
    zh: "本组课程须清楚说明受众、程度、语言与价格。不得批准误导性或明显偏离合理市场的课程。",
    ar: "يجب أن تصف عروض المجموعة بوضوح الجمهور والمستويات واللغة والسعر. لا تُوافق على عرض مضلل أو بعيد عن سوق معقول."
  },
  ExpertConduct_S7_Title: { fr: "Sécurité et signalement", en: "Safety and reporting", es: "Seguridad y denuncia", de: "Sicherheit und Meldung", pt: "Segurança e denúncia", zh: "安全与报告", ar: "السلامة والإبلاغ" },
  ExpertConduct_S7_Body: {
    fr: "Tout soupçon d'atteinte à un mineur ou à une personne vulnérable doit être signalé selon la loi applicable et aux administrateurs TutorSphere. La sécurité prime sur la confidentialité du groupe.",
    en: "Any suspicion of harm to a minor or vulnerable person must be reported under applicable law and to TutorSphere administrators. Safety prevails over group confidentiality.",
    es: "Cualquier sospecha de daño a un menor o persona vulnerable debe denunciarse según la ley y a los administradores de TutorSphere. La seguridad prima sobre la confidencialidad del grupo.",
    de: "Jeder Verdacht auf Schädigung Minderjähriger oder Schutzbedürftiger ist nach geltendem Recht und gegenüber TutorSphere-Administratoren zu melden. Sicherheit geht vor Gruppenvertraulichkeit.",
    pt: "Qualquer suspeita de dano a um menor ou pessoa vulnerável deve ser comunicada nos termos da lei e aos administradores TutorSphere. A segurança prevalece sobre a confidencialidade do grupo.",
    zh: "如怀疑未成年人或弱势者受到伤害，须依法并向 TutorSphere 管理员报告。安全优先于组内保密。",
    ar: "أي شبهة ضرر بقاصر أو شخص ضعيف تُبلَّغ وفق القانون ولإدارة TutorSphere. السلامة تعلو سرية المجموعة."
  },
  ExpertConduct_S8_Title: { fr: "Sanctions", en: "Sanctions", es: "Sanciones", de: "Sanktionen", pt: "Sanções", zh: "处分", ar: "الجزاءات" },
  ExpertConduct_S8_Body: {
    fr: "Un manquement peut entraîner avertissement, suspension du rôle Expert, exclusion du groupe ou clôture du compte, sans préjudice des recours légaux.",
    en: "A breach may lead to a warning, suspension of the Expert role, exclusion from the group or account closure, without prejudice to legal remedies.",
    es: "Un incumplimiento puede conllevar advertencia, suspensión del rol de experto, exclusión del grupo o cierre de la cuenta, sin perjuicio de recursos legales.",
    de: "Ein Verstoß kann Verwarnung, Sperrung der Expertenrolle, Ausschluss aus der Gruppe oder Kontoschließung nach sich ziehen, unbeschadet gesetzlicher Ansprüche.",
    pt: "Um incumprimento pode originar aviso, suspensão do papel de especialista, exclusão do grupo ou encerramento da conta, sem prejuízo de recursos legais.",
    zh: "违规可导致警告、暂停专家角色、除名或关闭账户，不影响法律救济。",
    ar: "قد يؤدي الإخلال إلى إنذار أو تعليق دور الخبير أو الاستبعاد من المجموعة أو إغلاق الحساب، دون مساس بالحقوق القانونية."
  },
  Privacy_Title: {
    fr: "Politique de confidentialité — Experts",
    en: "Privacy policy — Experts",
    es: "Política de privacidad — Expertos",
    de: "Datenschutzrichtlinie — Experten",
    pt: "Política de privacidade — Especialistas",
    zh: "隐私政策 — 专家",
    ar: "سياسة الخصوصية — الخبراء"
  },
  Privacy_Intro: {
    fr: "TutorSphere traite vos données pour créer votre compte Expert, gérer l'invitation, le vote et l'appartenance au groupe.",
    en: "TutorSphere processes your data to create your Expert account and to manage the invitation, the vote and group membership.",
    es: "TutorSphere trata sus datos para crear su cuenta de experto y gestionar la invitación, el voto y la pertenencia al grupo.",
    de: "TutorSphere verarbeitet Ihre Daten, um Ihr Expertenkonto anzulegen und Einladung, Abstimmung sowie Gruppenzugehörigkeit zu verwalten.",
    pt: "O TutorSphere trata os seus dados para criar a conta de especialista e gerir o convite, o voto e a pertença ao grupo.",
    zh: "TutorSphere 处理您的数据，用于创建专家账户并管理邀请、投票与入组。",
    ar: "تعالج TutorSphere بياناتك لإنشاء حساب الخبير وإدارة الدعوة والتصويت والعضوية."
  },
  Privacy_LegalNote: {
    fr: "Responsable de traitement : GISEBS / TutorSphere. Conservez une copie de cette page. Contactez le support pour exercer vos droits.",
    en: "Controller: GISEBS / TutorSphere. Keep a copy of this page. Contact support to exercise your rights.",
    es: "Responsable del tratamiento: GISEBS / TutorSphere. Conserve una copia. Contacte con el soporte para ejercer sus derechos.",
    de: "Verantwortlicher: GISEBS / TutorSphere. Bewahren Sie eine Kopie auf. Kontaktieren Sie den Support zur Ausübung Ihrer Rechte.",
    pt: "Responsável pelo tratamento: GISEBS / TutorSphere. Guarde uma cópia. Contacte o suporte para exercer os seus direitos.",
    zh: "控制者：GISEBS / TutorSphere。请自行保留本页副本。行使权利请联系支持。",
    ar: "المسؤول عن المعالجة: GISEBS / TutorSphere. احتفظ بنسخة. تواصل مع الدعم لممارسة حقوقك."
  },
  Privacy_AcceptCheckbox: {
    fr: "J'ai lu et j'accepte la politique de confidentialité TutorSphere applicable aux Experts (collecte, finalités, conservation, droits).",
    en: "I have read and I accept the TutorSphere privacy policy for Experts (collection, purposes, retention, rights).",
    es: "He leído y acepto la política de privacidad de TutorSphere para expertos (recogida, finalidades, conservación, derechos).",
    de: "Ich habe die TutorSphere-Datenschutzrichtlinie für Experten gelesen und akzeptiere sie (Erhebung, Zwecke, Speicherung, Rechte).",
    pt: "Li e aceito a política de privacidade TutorSphere para especialistas (recolha, finalidades, conservação, direitos).",
    zh: "我已阅读并接受适用于专家的 TutorSphere 隐私政策（收集、目的、保存、权利）。",
    ar: "قرأت وأقبل سياسة خصوصية TutorSphere للخبراء (الجمع، الأغراض، الحفظ، الحقوق)."
  },
  Privacy_Bullet_Data: {
    fr: "Données : identité, e-mail, spécialité, présentation, votes et statut d'admission.",
    en: "Data: identity, email, specialty, introduction, votes and admission status.",
    es: "Datos: identidad, correo, especialidad, presentación, votos y estado de admisión.",
    de: "Daten: Identität, E-Mail, Fachgebiet, Vorstellung, Stimmen und Aufnahmestatus.",
    pt: "Dados: identidade, e-mail, especialidade, apresentação, votos e estado de admissão.",
    zh: "数据：身份、邮箱、专长、简介、投票与录取状态。",
    ar: "البيانات: الهوية، البريد، التخصص، النبذة، الأصوات وحالة القبول."
  },
  Privacy_Bullet_Purpose: {
    fr: "Finalités : compte Expert, invitation, vote, gouvernance du groupe, sécurité de la plateforme.",
    en: "Purposes: Expert account, invitation, vote, group governance, platform security.",
    es: "Finalidades: cuenta de experto, invitación, voto, gobernanza del grupo, seguridad de la plataforma.",
    de: "Zwecke: Expertenkonto, Einladung, Abstimmung, Gruppenführung, Plattformsicherheit.",
    pt: "Finalidades: conta de especialista, convite, voto, governação do grupo, segurança da plataforma.",
    zh: "目的：专家账户、邀请、投票、组治理、平台安全。",
    ar: "الأغراض: حساب الخبير، الدعوة، التصويت، حوكمة المجموعة، أمن المنصة."
  },
  Privacy_Bullet_Rights: {
    fr: "Droits : accès, rectification, effacement lorsque la loi le permet, opposition et portabilité via le support TutorSphere.",
    en: "Rights: access, rectification, erasure where the law allows, objection and portability via TutorSphere support.",
    es: "Derechos: acceso, rectificación, supresión cuando la ley lo permita, oposición y portabilidad a través del soporte.",
    de: "Rechte: Auskunft, Berichtigung, Löschung soweit gesetzlich zulässig, Widerspruch und Datenübertragbarkeit über den Support.",
    pt: "Direitos: acesso, retificação, apagamento quando a lei o permitir, oposição e portabilidade através do suporte.",
    zh: "权利：查阅、更正、在法律允许时删除、反对及通过支持进行可携。",
    ar: "الحقوق: الوصول والتصحيح والمحو حيث يسمح القانون والاعتراض والنقل عبر دعم TutorSphere."
  },
  Privacy_S1_Title: { fr: "Données collectées", en: "Data collected", es: "Datos recogidos", de: "Erhobene Daten", pt: "Dados recolhidos", zh: "收集的数据", ar: "البيانات المجمّعة" },
  Privacy_S1_Body: {
    fr: "Nous collectons les informations fournies dans le formulaire d'invitation (nom, e-mail, spécialité, présentation, mot de passe haché) ainsi que les métadonnées techniques de connexion raisonnablement nécessaires à la sécurité.",
    en: "We collect the information provided in the invitation form (name, email, specialty, introduction, hashed password) and technical connection metadata reasonably needed for security.",
    es: "Recogemos la información del formulario de invitación (nombre, correo, especialidad, presentación, contraseña cifrada) y metadatos técnicos razonablemente necesarios para la seguridad.",
    de: "Wir erheben die Angaben im Einladungsformular (Name, E-Mail, Fachgebiet, Vorstellung, gehashtes Passwort) sowie technisch erforderliche Verbindungsdaten zur Sicherheit.",
    pt: "Recolhemos as informações do formulário de convite (nome, e-mail, especialidade, apresentação, palavra-passe cifrada) e metadados técnicos razoavelmente necessários à segurança.",
    zh: "我们收集邀请表中的信息（姓名、邮箱、专长、简介、散列密码）以及为安全所需的合理连接元数据。",
    ar: "نجمع معلومات نموذج الدعوة (الاسم، البريد، التخصص، النبذة، كلمة مرور مرمّزة) وبيانات الاتصال التقنية اللازمة للأمن."
  },
  Privacy_S2_Title: { fr: "Finalités et base", en: "Purposes and legal basis", es: "Finalidades y base", de: "Zwecke und Rechtsgrundlage", pt: "Finalidades e base", zh: "目的与法律依据", ar: "الأغراض والأساس القانوني" },
  Privacy_S2_Body: {
    fr: "Le traitement est nécessaire à l'exécution de votre candidature (contrat) et à l'intérêt légitime de sécuriser la gouvernance des groupes d'experts.",
    en: "Processing is necessary to perform your application (contract) and for the legitimate interest of securing expert-group governance.",
    es: "El tratamiento es necesario para ejecutar su candidatura (contrato) y por interés legítimo de asegurar la gobernanza de los grupos.",
    de: "Die Verarbeitung ist zur Durchführung Ihrer Bewerbung (Vertrag) und aus berechtigtem Interesse an der sicheren Gruppenführung erforderlich.",
    pt: "O tratamento é necessário para executar a candidatura (contrato) e por interesse legítimo em assegurar a governação dos grupos.",
    zh: "处理为履行您的申请（合同）以及保障专家组治理的正当利益所必需。",
    ar: "المعالجة لازمة لتنفيذ ترشيحك (عقد) وللمصلحة المشروعة في تأمين حوكمة مجموعات الخبراء."
  },
  Privacy_S3_Title: { fr: "Conservation", en: "Retention", es: "Conservación", de: "Speicherung", pt: "Conservação", zh: "保存期限", ar: "الحفظ" },
  Privacy_S3_Body: {
    fr: "Les données d'invitation et de vote sont conservées le temps de la procédure puis archivées selon les durées légales et de preuve de la plateforme.",
    en: "Invitation and vote data are kept for the duration of the procedure, then archived according to the platform's legal and evidential retention periods.",
    es: "Los datos de invitación y voto se conservan durante el procedimiento y luego se archivan según los plazos legales y de prueba.",
    de: "Einladungs- und Abstimmungsdaten werden für das Verfahren gespeichert und anschließend gemäß gesetzlicher und nachweislicher Fristen archiviert.",
    pt: "Os dados de convite e voto conservam-se durante o procedimento e depois arquivam-se segundo prazos legais e de prova.",
    zh: "邀请与投票数据在流程期间保存，随后按平台法定与举证期限归档。",
    ar: "تُحفظ بيانات الدعوة والتصويت طوال الإجراء ثم تُؤرشف وفق المدد القانونية والإثباتية للمنصة."
  },
  Privacy_S4_Title: { fr: "Destinataires", en: "Recipients", es: "Destinatarios", de: "Empfänger", pt: "Destinatários", zh: "接收方", ar: "المستلمون" },
  Privacy_S4_Body: {
    fr: "Les membres votants du groupe, le Responsable et les administrateurs plateforme habilités peuvent voir les informations nécessaires à l'admission. Nous ne vendons pas vos données.",
    en: "Voting members, the group manager and authorised platform administrators may see the information needed for admission. We do not sell your data.",
    es: "Los miembros votantes, el responsable y los administradores habilitados pueden ver la información necesaria para la admisión. No vendemos sus datos.",
    de: "Stimmberechtigte Mitglieder, der Gruppenleiter und befugte Plattform-Administratoren können die zur Aufnahme nötigen Angaben sehen. Wir verkaufen Ihre Daten nicht.",
    pt: "Os membros votantes, o responsável e os administradores habilitados podem ver as informações necessárias à admissão. Não vendemos os seus dados.",
    zh: "投票成员、负责人及获授权的平台管理员可查看录取所需信息。我们不出售您的数据。",
    ar: "يمكن للأعضاء المصوّتين والمسؤول ومديري المنصة المخوّلين رؤية المعلومات اللازمة للقبول. لا نبيع بياناتك."
  },
  Privacy_S5_Title: { fr: "Vos droits", en: "Your rights", es: "Sus derechos", de: "Ihre Rechte", pt: "Os seus direitos", zh: "您的权利", ar: "حقوقك" },
  Privacy_S5_Body: {
    fr: "Vous pouvez demander l'accès, la correction ou, lorsque la loi le permet, l'effacement. Certaines données de gouvernance (votes, décisions) peuvent être conservées pour la preuve et la sécurité.",
    en: "You may request access, correction or, where the law allows, erasure. Some governance data (votes, decisions) may be kept for evidence and security.",
    es: "Puede solicitar acceso, corrección o, cuando la ley lo permita, supresión. Algunos datos de gobernanza (votos, decisiones) pueden conservarse como prueba y seguridad.",
    de: "Sie können Auskunft, Berichtigung oder, soweit zulässig, Löschung verlangen. Bestimmte Governance-Daten (Stimmen, Entscheidungen) können zu Beweis- und Sicherheitszwecken gespeichert bleiben.",
    pt: "Pode pedir acesso, correção ou, quando a lei o permitir, apagamento. Alguns dados de governação (votos, decisões) podem conservar-se para prova e segurança.",
    zh: "您可以申请查阅、更正，并在法律允许时删除。部分治理数据（投票、决定）可能因举证与安全而保留。",
    ar: "يمكنك طلب الوصول أو التصحيح أو المحو حيث يسمح القانون. قد تُحفظ بعض بيانات الحوكمة (الأصوات، القرارات) للإثبات والأمن."
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
  return String(s)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

const files = [
  "SharedResources.resx",
  "SharedResources.fr.resx",
  "SharedResources.en.resx",
  "SharedResources.es.resx",
  "SharedResources.de.resx",
  "SharedResources.pt.resx",
  "SharedResources.zh-Hans.resx",
  "SharedResources.ar.resx"
];

for (const file of files) {
  const p = path.join(__dirname, file);
  let xml = fs.readFileSync(p, "utf8");
  let added = 0;
  const blocks = [];
  for (const [k, v] of Object.entries(KEYS)) {
    if (xml.includes(`name="${k}"`)) continue;
    const val = pick(v, file);
    blocks.push(`  <data name="${k}" xml:space="preserve">\n    <value>${enc(val)}</value>\n  </data>`);
    added++;
  }
  if (blocks.length) {
    xml = xml.replace(/\s*<\/root>\s*$/, `\n${blocks.join("\n")}\n</root>\n`);
    fs.writeFileSync(p, xml, "utf8");
  }
  console.log(file, "added", added);
}
