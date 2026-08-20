namespace TutorSphere.Application.Common;

/// <summary>
/// Message d'invitation d'un enseignant, prêt à coller dans WhatsApp, un SMS ou un courriel.
/// Le destinataire d'un lien partageable est inconnu du système : c'est l'expert qui choisit la
/// langue, une par langue prise en charge par l'interface.
/// Texte brut et non HTML : ces messages partent par WhatsApp ou SMS, où le balisage ne rend rien.
/// </summary>
public static class TeacherInviteShareMessage
{
    /// <param name="expiresUtc">Date d'expiration du lien, rendue dans le calendrier de la langue.</param>
    /// <param name="personalMessage">
    /// Phrase d'accueil libre de l'expert. Elle forme son propre paragraphe : insérée au fil du
    /// texte institutionnel, elle se lisait comme une coquille.
    /// </param>
    public static string Build(
        string? language,
        string groupName,
        string senderName,
        bool senderIsManager,
        string applyUrl,
        DateTime expiresUtc,
        string? personalMessage)
    {
        var lang = SupportedLanguageCodes.Normalize(language);
        var expires = expiresUtc.ToString("d", SupportedLanguageCodes.GetCulture(lang));
        var note = string.IsNullOrWhiteSpace(personalMessage)
            ? ""
            : personalMessage.Trim() + "\n\n";

        return lang switch
        {
            SupportedLanguageCodes.English => English(groupName, senderName, senderIsManager, applyUrl, expires, note),
            SupportedLanguageCodes.Spanish => Spanish(groupName, senderName, senderIsManager, applyUrl, expires, note),
            SupportedLanguageCodes.German => German(groupName, senderName, senderIsManager, applyUrl, expires, note),
            SupportedLanguageCodes.Portuguese => Portuguese(groupName, senderName, senderIsManager, applyUrl, expires, note),
            SupportedLanguageCodes.MandarinChinese => Chinese(groupName, senderName, senderIsManager, applyUrl, expires, note),
            SupportedLanguageCodes.Arabic => Arabic(groupName, senderName, senderIsManager, applyUrl, expires, note),
            _ => French(groupName, senderName, senderIsManager, applyUrl, expires, note)
        };
    }

    /// <summary>Objet du courriel, dans la langue du message.</summary>
    public static string Subject(string? language) =>
        SupportedLanguageCodes.Normalize(language) switch
        {
            SupportedLanguageCodes.English => "Invitation to join TutorSphere as a teacher",
            SupportedLanguageCodes.Spanish => "Invitación para unirse a TutorSphere como docente",
            SupportedLanguageCodes.German => "Einladung, TutorSphere als Lehrkraft beizutreten",
            SupportedLanguageCodes.Portuguese => "Convite para se juntar ao TutorSphere como professor",
            SupportedLanguageCodes.MandarinChinese => "邀请您以教师身份加入 TutorSphere",
            SupportedLanguageCodes.Arabic => "دعوة للانضمام إلى TutorSphere بصفة معلّم",
            _ => "Invitation à rejoindre TutorSphere en tant qu’enseignant"
        };

    /// <summary>Nom de la langue dans la langue elle-même, pour le sélecteur.</summary>
    public static string LanguageLabel(string? language) =>
        SupportedLanguageCodes.Normalize(language) switch
        {
            SupportedLanguageCodes.English => "English",
            SupportedLanguageCodes.Spanish => "Español",
            SupportedLanguageCodes.German => "Deutsch",
            SupportedLanguageCodes.Portuguese => "Português",
            SupportedLanguageCodes.MandarinChinese => "中文（简体）",
            SupportedLanguageCodes.Arabic => "العربية",
            _ => "Français"
        };

    private static string French(
        string group, string sender, bool isManager, string url, string expires, string note) =>
        "Bonjour,\n\n" +
        $"Le groupe d’experts {group} a le plaisir de vous inviter à rejoindre TutorSphere " +
        "en qualité d’enseignant.\n\n" +
        "TutorSphere est une plateforme de soutien scolaire en ligne développée par " +
        "Groupe GISEBS Inc., une entreprise canadienne spécialisée dans les technologies et " +
        "les solutions numériques appliquées notamment à l’éducation.\n\n" +
        "Notre mission est simple : permettre aux enfants, où qu’ils se trouvent, d’accéder à " +
        "des enseignants qualifiés capables de les accompagner à distance, de leur expliquer " +
        "les notions difficiles et de les aider à progresser avec confiance.\n\n" +
        "En rejoignant TutorSphere, vous pourrez :\n\n" +
        "• transmettre vos connaissances à des élèves au Canada, aux États-Unis et ailleurs dans le monde ;\n" +
        "• enseigner à distance depuis votre domicile ;\n" +
        "• présenter vos matières et les niveaux que vous maîtrisez ;\n" +
        "• choisir vos disponibilités ;\n" +
        "• définir vos tarifs selon les conditions applicables ;\n" +
        "• participer à une communauté d’enseignants engagés pour la réussite des enfants.\n\n" +
        "Votre expérience et vos compétences pédagogiques peuvent véritablement contribuer au " +
        "parcours scolaire d’un enfant.\n\n" +
        "La création de votre profil est simple, sécurisée et ne prend que quelques minutes.\n\n" +
        "👉 Créez votre profil enseignant à partir de ce lien :\n\n" +
        $"{url}\n\n" +
        $"Ce lien vous est adressé par le groupe d’experts {group} et reste valable jusqu’au " +
        $"{expires}. Merci de le réserver aux enseignants à qui cette invitation est destinée.\n\n" +
        note +
        $"Votre candidature sera étudiée par le groupe d’experts {group}. Une fois votre " +
        "profil approuvé, vous pourrez accéder à votre espace enseignant et commencer à " +
        "proposer vos services sur TutorSphere.\n\n" +
        "Nous serions honorés de vous compter parmi les enseignants qui contribuent à rendre " +
        "l’accompagnement scolaire plus accessible aux enfants.\n\n" +
        "Cordialement,\n\n" +
        $"{sender}\n" +
        $"{(isManager ? "Responsable du groupe d’experts" : "Groupe d’experts")} {group}\n" +
        "TutorSphere — Groupe GISEBS Inc.";

    private static string English(
        string group, string sender, bool isManager, string url, string expires, string note) =>
        "Hello,\n\n" +
        $"The {group} expert group is pleased to invite you to join TutorSphere as a teacher.\n\n" +
        "TutorSphere is an online tutoring platform developed by Groupe GISEBS Inc., a Canadian " +
        "company specialising in technology and digital solutions, applied in particular to " +
        "education.\n\n" +
        "Our mission is simple: to give children, wherever they are, access to qualified teachers " +
        "who can support them remotely, explain difficult concepts and help them make progress " +
        "with confidence.\n\n" +
        "By joining TutorSphere, you will be able to:\n\n" +
        "• share your knowledge with students in Canada, the United States and elsewhere in the world;\n" +
        "• teach remotely from home;\n" +
        "• present the subjects and grade levels you master;\n" +
        "• choose your own availability;\n" +
        "• set your rates in accordance with the applicable terms;\n" +
        "• take part in a community of teachers committed to children’s success.\n\n" +
        "Your experience and your teaching skills can genuinely shape a child’s schooling.\n\n" +
        "Creating your profile is simple, secure and takes only a few minutes.\n\n" +
        "👉 Create your teacher profile using this link:\n\n" +
        $"{url}\n\n" +
        $"This link is sent to you by the {group} expert group and remains valid until {expires}. " +
        "Please keep it for the teachers this invitation is intended for.\n\n" +
        note +
        $"Your application will be reviewed by the {group} expert group. Once your profile is " +
        "approved, you will be able to access your teacher workspace and start offering your " +
        "services on TutorSphere.\n\n" +
        "We would be honoured to count you among the teachers who help make academic support more " +
        "accessible to children.\n\n" +
        "Kind regards,\n\n" +
        $"{sender}\n" +
        $"{(isManager ? $"Manager of the {group} expert group" : $"{group} expert group")}\n" +
        "TutorSphere — Groupe GISEBS Inc.";

    private static string Spanish(
        string group, string sender, bool isManager, string url, string expires, string note) =>
        "Buenos días:\n\n" +
        $"El grupo de expertos {group} tiene el placer de invitarle a unirse a TutorSphere en " +
        "calidad de docente.\n\n" +
        "TutorSphere es una plataforma de apoyo escolar en línea desarrollada por " +
        "Groupe GISEBS Inc., una empresa canadiense especializada en tecnologías y soluciones " +
        "digitales aplicadas, en particular, a la educación.\n\n" +
        "Nuestra misión es sencilla: permitir que los niños, dondequiera que se encuentren, " +
        "accedan a docentes cualificados capaces de acompañarlos a distancia, de explicarles las " +
        "nociones difíciles y de ayudarlos a progresar con confianza.\n\n" +
        "Al unirse a TutorSphere, podrá:\n\n" +
        "• transmitir sus conocimientos a alumnos de Canadá, Estados Unidos y otros países del mundo;\n" +
        "• enseñar a distancia desde su domicilio;\n" +
        "• presentar las materias y los niveles que domina;\n" +
        "• elegir su disponibilidad;\n" +
        "• fijar sus tarifas según las condiciones aplicables;\n" +
        "• participar en una comunidad de docentes comprometidos con el éxito de los niños.\n\n" +
        "Su experiencia y sus competencias pedagógicas pueden contribuir verdaderamente al " +
        "recorrido escolar de un niño.\n\n" +
        "La creación de su perfil es sencilla, segura y solo toma unos minutos.\n\n" +
        "👉 Cree su perfil de docente a partir de este enlace:\n\n" +
        $"{url}\n\n" +
        $"Este enlace se lo envía el grupo de expertos {group} y es válido hasta el {expires}. " +
        "Le agradecemos que lo reserve a los docentes a quienes está destinada esta invitación.\n\n" +
        note +
        $"Su candidatura será estudiada por el grupo de expertos {group}. Una vez aprobado su " +
        "perfil, podrá acceder a su espacio de docente y comenzar a ofrecer sus servicios en " +
        "TutorSphere.\n\n" +
        "Nos sentiríamos honrados de contarle entre los docentes que contribuyen a hacer el apoyo " +
        "escolar más accesible para los niños.\n\n" +
        "Atentamente,\n\n" +
        $"{sender}\n" +
        $"{(isManager ? $"Responsable del grupo de expertos {group}" : $"Grupo de expertos {group}")}\n" +
        "TutorSphere — Groupe GISEBS Inc.";

    private static string German(
        string group, string sender, bool isManager, string url, string expires, string note) =>
        "Guten Tag,\n\n" +
        $"die Expertengruppe {group} freut sich, Sie als Lehrkraft zu TutorSphere einzuladen.\n\n" +
        "TutorSphere ist eine Online-Nachhilfeplattform der Groupe GISEBS Inc., eines kanadischen " +
        "Unternehmens, das sich auf Technologien und digitale Lösungen insbesondere für den " +
        "Bildungsbereich spezialisiert hat.\n\n" +
        "Unser Auftrag ist einfach: Kindern – wo auch immer sie leben – Zugang zu qualifizierten " +
        "Lehrkräften geben, die sie aus der Ferne begleiten, ihnen schwierige Inhalte erklären und " +
        "ihnen helfen, mit Zuversicht Fortschritte zu machen.\n\n" +
        "Bei TutorSphere können Sie:\n\n" +
        "• Ihr Wissen an Schülerinnen und Schüler in Kanada, den Vereinigten Staaten und weltweit weitergeben;\n" +
        "• von Ihrem Zuhause aus im Fernunterricht unterrichten;\n" +
        "• Ihre Fächer und die von Ihnen beherrschten Klassenstufen darstellen;\n" +
        "• Ihre Verfügbarkeiten selbst festlegen;\n" +
        "• Ihre Honorare nach den geltenden Bedingungen bestimmen;\n" +
        "• Teil einer Gemeinschaft von Lehrkräften werden, die sich für den Erfolg der Kinder einsetzen.\n\n" +
        "Ihre Erfahrung und Ihre pädagogischen Fähigkeiten können den Bildungsweg eines Kindes " +
        "wirklich prägen.\n\n" +
        "Die Erstellung Ihres Profils ist einfach, sicher und dauert nur wenige Minuten.\n\n" +
        "👉 Erstellen Sie Ihr Lehrkraft-Profil über diesen Link:\n\n" +
        $"{url}\n\n" +
        $"Diesen Link erhalten Sie von der Expertengruppe {group}; er ist bis zum {expires} " +
        "gültig. Bitte geben Sie ihn nur an die Lehrkräfte weiter, für die diese Einladung " +
        "bestimmt ist.\n\n" +
        note +
        $"Ihre Bewerbung wird von der Expertengruppe {group} geprüft. Sobald Ihr Profil " +
        "freigegeben ist, erhalten Sie Zugang zu Ihrem Lehrkraft-Bereich und können Ihre " +
        "Leistungen auf TutorSphere anbieten.\n\n" +
        "Es wäre eine Ehre, Sie zu den Lehrkräften zu zählen, die schulische Unterstützung für " +
        "Kinder zugänglicher machen.\n\n" +
        "Mit freundlichen Grüßen\n\n" +
        $"{sender}\n" +
        $"{(isManager ? $"Leitung der Expertengruppe {group}" : $"Expertengruppe {group}")}\n" +
        "TutorSphere — Groupe GISEBS Inc.";

    private static string Portuguese(
        string group, string sender, bool isManager, string url, string expires, string note) =>
        "Olá,\n\n" +
        $"O grupo de especialistas {group} tem o prazer de o convidar a juntar-se ao TutorSphere " +
        "na qualidade de professor.\n\n" +
        "O TutorSphere é uma plataforma de apoio escolar em linha desenvolvida pela " +
        "Groupe GISEBS Inc., uma empresa canadiana especializada em tecnologias e soluções " +
        "digitais aplicadas, em particular, à educação.\n\n" +
        "A nossa missão é simples: permitir que as crianças, onde quer que estejam, tenham acesso " +
        "a professores qualificados capazes de as acompanhar à distância, de lhes explicar as " +
        "noções difíceis e de as ajudar a progredir com confiança.\n\n" +
        "Ao juntar-se ao TutorSphere, poderá:\n\n" +
        "• transmitir os seus conhecimentos a alunos no Canadá, nos Estados Unidos e noutros países do mundo;\n" +
        "• ensinar à distância a partir de casa;\n" +
        "• apresentar as disciplinas e os níveis que domina;\n" +
        "• escolher as suas disponibilidades;\n" +
        "• definir os seus honorários de acordo com as condições aplicáveis;\n" +
        "• participar numa comunidade de professores empenhados no sucesso das crianças.\n\n" +
        "A sua experiência e as suas competências pedagógicas podem contribuir verdadeiramente " +
        "para o percurso escolar de uma criança.\n\n" +
        "A criação do seu perfil é simples, segura e leva apenas alguns minutos.\n\n" +
        "👉 Crie o seu perfil de professor a partir deste link:\n\n" +
        $"{url}\n\n" +
        $"Este link é-lhe enviado pelo grupo de especialistas {group} e é válido até {expires}. " +
        "Agradecemos que o reserve aos professores a quem este convite se destina.\n\n" +
        note +
        $"A sua candidatura será analisada pelo grupo de especialistas {group}. Depois de o seu " +
        "perfil ser aprovado, poderá aceder ao seu espaço de professor e começar a propor os seus " +
        "serviços no TutorSphere.\n\n" +
        "Ficaríamos honrados por o contar entre os professores que ajudam a tornar o apoio escolar " +
        "mais acessível às crianças.\n\n" +
        "Com os melhores cumprimentos,\n\n" +
        $"{sender}\n" +
        $"{(isManager ? $"Responsável do grupo de especialistas {group}" : $"Grupo de especialistas {group}")}\n" +
        "TutorSphere — Groupe GISEBS Inc.";

    private static string Chinese(
        string group, string sender, bool isManager, string url, string expires, string note) =>
        "您好，\n\n" +
        $"{group} 专家组诚挚邀请您以教师身份加入 TutorSphere。\n\n" +
        "TutorSphere 是由加拿大企业 Groupe GISEBS Inc. 开发的在线课业辅导平台，该公司专注于技术与数字化解决方案，" +
        "并特别应用于教育领域。\n\n" +
        "我们的使命很简单：让孩子无论身在何处，都能找到合格的教师，通过远程方式陪伴他们、讲解难点，" +
        "帮助他们自信地取得进步。\n\n" +
        "加入 TutorSphere 后，您可以：\n\n" +
        "• 向加拿大、美国以及世界各地的学生传授您的知识；\n" +
        "• 在家中进行远程授课；\n" +
        "• 展示您擅长的科目与教学阶段；\n" +
        "• 自行安排可授课时间；\n" +
        "• 在适用条件下自行设定收费标准；\n" +
        "• 加入一个致力于帮助孩子取得成功的教师社群。\n\n" +
        "您的经验与教学能力，能够为一个孩子的学习历程带来真正的改变。\n\n" +
        "创建个人档案简单、安全，只需几分钟。\n\n" +
        "👉 请通过以下链接创建您的教师档案：\n\n" +
        $"{url}\n\n" +
        $"此链接由 {group} 专家组发送，有效期至 {expires}。请仅将其提供给本邀请所指定的教师。\n\n" +
        note +
        $"您的申请将由 {group} 专家组审核。档案通过审核后，您即可进入教师工作空间，" +
        "并开始在 TutorSphere 上提供教学服务。\n\n" +
        "如能邀您加入这些让课业辅导更易触及孩子的教师之列，我们将深感荣幸。\n\n" +
        "顺致敬意，\n\n" +
        $"{sender}\n" +
        $"{(isManager ? $"{group} 专家组负责人" : $"{group} 专家组")}\n" +
        "TutorSphere — Groupe GISEBS Inc.";

    private static string Arabic(
        string group, string sender, bool isManager, string url, string expires, string note) =>
        "تحية طيبة،\n\n" +
        $"يسرّ مجموعة الخبراء {group} أن تدعوكم للانضمام إلى TutorSphere بصفة معلّم.\n\n" +
        "TutorSphere منصّة للدعم الدراسي عبر الإنترنت طوّرتها شركة Groupe GISEBS Inc.، " +
        "وهي شركة كندية متخصّصة في التقنيات والحلول الرقمية، ولا سيّما في مجال التعليم.\n\n" +
        "مهمّتنا بسيطة: تمكين الأطفال، أينما كانوا، من الوصول إلى معلّمين مؤهّلين قادرين على " +
        "مواكبتهم عن بُعد، وشرح المفاهيم الصعبة لهم، ومساعدتهم على التقدّم بثقة.\n\n" +
        "بانضمامكم إلى TutorSphere، سيكون في مقدوركم:\n\n" +
        "• نقل معارفكم إلى تلاميذ في كندا والولايات المتحدة وسائر أنحاء العالم؛\n" +
        "• التدريس عن بُعد من منزلكم؛\n" +
        "• عرض المواد والمستويات التي تتمكّنون منها؛\n" +
        "• تحديد أوقات توافركم؛\n" +
        "• تحديد أسعاركم وفق الشروط المعمول بها؛\n" +
        "• الانتماء إلى مجتمع من المعلّمين الملتزمين بنجاح الأطفال.\n\n" +
        "إنّ خبرتكم وكفاءاتكم التربوية قادرة فعلاً على الإسهام في المسار الدراسي لطفل.\n\n" +
        "إنشاء ملفكم الشخصي بسيط وآمن ولا يستغرق سوى دقائق قليلة.\n\n" +
        "👉 أنشئوا ملفكم كمعلّم من خلال هذا الرابط:\n\n" +
        $"{url}\n\n" +
        $"هذا الرابط مُرسَل إليكم من مجموعة الخبراء {group} وصالح حتى {expires}. " +
        "نرجو قصْره على المعلّمين المقصودين بهذه الدعوة.\n\n" +
        note +
        $"سيَدرُس طلبكم مجموعة الخبراء {group}. وبعد الموافقة على ملفكم، يصبح في مقدوركم الدخول " +
        "إلى مساحتكم كمعلّم والبدء بتقديم خدماتكم على TutorSphere.\n\n" +
        "يشرّفنا أن تكونوا بين المعلّمين الذين يسهمون في جعل الدعم الدراسي أقرب إلى الأطفال.\n\n" +
        "وتفضّلوا بقبول فائق التقدير،\n\n" +
        $"{sender}\n" +
        $"{(isManager ? $"مسؤول مجموعة الخبراء {group}" : $"مجموعة الخبراء {group}")}\n" +
        "TutorSphere — Groupe GISEBS Inc.";
}
