using System.Text.Json;
using System.Text.RegularExpressions;

namespace TutorSphere.Application.Services;

public sealed record TeacherContractSectionDef(string Key, string Title, string Body);

public static class TeacherContractCatalog
{
    public const string CurrentVersion = "2026.1";

    public static IReadOnlyList<string> VariableKeys { get; } =
    [
        "GROUP_LEGAL_FORM", "GROUP_REGISTRATION_NUMBER", "GROUP_ADDRESS", "GROUP_ADMIN_ROLE",
        "TEACHER_DATE_OF_BIRTH", "TEACHER_ADDRESS",
        "CANCELLATION_NOTICE", "LATE_TOLERANCE", "UNJUSTIFIED_ABSENCE_RULE",
        "COMPENSATION_RATE", "CURRENCY", "PAYMENT_FREQUENCY", "PAYMENT_METHOD", "APPLICABLE_FEES",
        "PAYMENT_DISPUTE_PERIOD", "NON_SOLICITATION_PERIOD", "INTELLECTUAL_PROPERTY_RULE",
        "CONTRACT_DURATION_TYPE", "CONTRACT_END_DATE", "TERMINATION_NOTICE",
        "AMICABLE_RESOLUTION_PERIOD", "DISPUTE_RESOLUTION_METHOD",
        "GOVERNING_JURISDICTION", "COMPETENT_COURT_LOCATION"
    ];

    public static IReadOnlyDictionary<string, string> VariableLabels { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GROUP_LEGAL_FORM"] = "Forme juridique du groupe",
        ["GROUP_REGISTRATION_NUMBER"] = "Numéro d’enregistrement",
        ["GROUP_ADDRESS"] = "Adresse du groupe",
        ["GROUP_ADMIN_ROLE"] = "Fonction du représentant",
        ["TEACHER_DATE_OF_BIRTH"] = "Date de naissance de l’enseignant",
        ["TEACHER_ADDRESS"] = "Adresse de l’enseignant",
        ["CANCELLATION_NOTICE"] = "Délai minimal d’annulation",
        ["LATE_TOLERANCE"] = "Tolérance de retard",
        ["UNJUSTIFIED_ABSENCE_RULE"] = "Conséquence d’une absence injustifiée",
        ["COMPENSATION_RATE"] = "Taux ou formule de rémunération",
        ["CURRENCY"] = "Devise",
        ["PAYMENT_FREQUENCY"] = "Fréquence de paiement",
        ["PAYMENT_METHOD"] = "Moyen de paiement",
        ["APPLICABLE_FEES"] = "Commission ou frais applicables",
        ["PAYMENT_DISPUTE_PERIOD"] = "Délai de contestation (jours)",
        ["NON_SOLICITATION_PERIOD"] = "Durée de non-sollicitation",
        ["INTELLECTUAL_PROPERTY_RULE"] = "Propriété des contenus créés pour le Groupe",
        ["CONTRACT_DURATION_TYPE"] = "Durée du contrat",
        ["CONTRACT_END_DATE"] = "Date de fin (si durée déterminée)",
        ["TERMINATION_NOTICE"] = "Préavis de résiliation (jours)",
        ["AMICABLE_RESOLUTION_PERIOD"] = "Délai de règlement amiable (jours)",
        ["DISPUTE_RESOLUTION_METHOD"] = "Mécanisme de règlement des différends",
        ["GOVERNING_JURISDICTION"] = "Droit applicable",
        ["COMPETENT_COURT_LOCATION"] = "Tribunaux compétents"
    };

    public static Dictionary<string, string> DefaultVariables() => new(StringComparer.Ordinal)
    {
        ["GROUP_LEGAL_FORM"] = "—",
        ["GROUP_REGISTRATION_NUMBER"] = "—",
        ["GROUP_ADDRESS"] = "—",
        ["GROUP_ADMIN_ROLE"] = "Responsable du groupe",
        ["TEACHER_DATE_OF_BIRTH"] = "—",
        ["TEACHER_ADDRESS"] = "—",
        ["CANCELLATION_NOTICE"] = "24 heures",
        ["LATE_TOLERANCE"] = "10 minutes",
        ["UNJUSTIFIED_ABSENCE_RULE"] = "Le cours peut être considéré comme non fourni et non rémunéré.",
        ["COMPENSATION_RATE"] = "Selon les tarifs acceptés dans TutorSphere",
        ["CURRENCY"] = "CAD",
        ["PAYMENT_FREQUENCY"] = "Mensuelle",
        ["PAYMENT_METHOD"] = "Virement / moyen enregistré dans TutorSphere",
        ["APPLICABLE_FEES"] = "Selon la politique du Groupe affichée dans TutorSphere",
        ["PAYMENT_DISPUTE_PERIOD"] = "15",
        ["NON_SOLICITATION_PERIOD"] = "12 mois",
        ["INTELLECTUAL_PROPERTY_RULE"] = "au Groupe, pour les usages liés aux services concernés",
        ["CONTRACT_DURATION_TYPE"] = "à durée indéterminée",
        ["CONTRACT_END_DATE"] = "—",
        ["TERMINATION_NOTICE"] = "15",
        ["AMICABLE_RESOLUTION_PERIOD"] = "30",
        ["DISPUTE_RESOLUTION_METHOD"] = "médiation, puis tribunaux compétents",
        ["GOVERNING_JURISDICTION"] = "la province ou le pays du siège du Groupe",
        ["COMPETENT_COURT_LOCATION"] = "le ressort du siège du Groupe"
    };

    public static IReadOnlyList<TeacherContractSectionDef> Sections(string language)
    {
        _ = language;
        return FrenchSections();
    }

    public static string Fill(string template, IReadOnlyDictionary<string, string> values)
    {
        return Regex.Replace(template, @"\{\{([A-Z0-9_]+)\}\}", m =>
        {
            var key = m.Groups[1].Value;
            return values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : "—";
        });
    }

    public static Dictionary<string, string> ParsePlaceholders(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return DefaultVariables();
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            var merged = DefaultVariables();
            if (parsed is null) return merged;
            foreach (var (k, v) in parsed)
                merged[k] = v ?? "—";
            return merged;
        }
        catch
        {
            return DefaultVariables();
        }
    }

    private static IReadOnlyList<TeacherContractSectionDef> FrenchSections() =>
    [
        new("parties", "Les parties",
            "### 1. Le groupe ou l’établissement\n\n" +
            "**Nom :** {{GROUP_NAME}}\n**Forme juridique :** {{GROUP_LEGAL_FORM}}\n**Numéro d’enregistrement :** {{GROUP_REGISTRATION_NUMBER}}\n" +
            "**Adresse :** {{GROUP_ADDRESS}}\n**Représenté par :** {{GROUP_ADMIN_NAME}}\n**Fonction :** {{GROUP_ADMIN_ROLE}}\n\n" +
            "Ci-après désigné « le Groupe ».\n\n" +
            "### 2. L’enseignant\n\n" +
            "**Nom et prénom :** {{TEACHER_FULL_NAME}}\n**Date de naissance :** {{TEACHER_DATE_OF_BIRTH}}\n**Adresse :** {{TEACHER_ADDRESS}}\n" +
            "**Pays de résidence :** {{TEACHER_COUNTRY}}\n**Courriel :** {{TEACHER_EMAIL}}\n**Téléphone :** {{TEACHER_PHONE}}\n" +
            "**Matières enseignées :** {{TEACHER_SUBJECTS}}\n**Niveaux scolaires :** {{TEACHER_LEVELS}}\n\n" +
            "Ci-après désigné « l’Enseignant ».\n\nLe Groupe et l’Enseignant sont collectivement désignés « les Parties »."),

        new("preamble", "Préambule",
            "Le Groupe utilise la plateforme **TutorSphere** afin d’organiser et de fournir des services de soutien scolaire, de tutorat et d’accompagnement pédagogique.\n\n" +
            "L’Enseignant déclare posséder les compétences, les qualifications et l’expérience nécessaires pour assurer les services pédagogiques qui lui seront confiés.\n\n" +
            "Les Parties souhaitent définir, par le présent contrat, les conditions de leur collaboration."),

        new("art1", "Article 1 — Objet du contrat",
            "Le présent contrat a pour objet de définir les conditions dans lesquelles l’Enseignant fournit des services de tutorat, de soutien scolaire ou d’accompagnement pédagogique aux élèves inscrits auprès du Groupe sur TutorSphere.\n\n" +
            "Les services peuvent notamment comprendre : la préparation et l’animation de cours individuels ou collectifs ; l’évaluation des besoins pédagogiques des élèves ; la préparation de supports de cours ; la création, la correction et le suivi des devoirs ; la rédaction de rapports de progression ; la communication pédagogique avec les parents ou responsables légaux ; la participation aux réunions organisées par le Groupe ; toute autre activité pédagogique acceptée par les Parties."),

        new("art2", "Article 2 — Statut de l’enseignant",
            "Sauf disposition contraire imposée par la législation applicable, l’Enseignant intervient à titre de professionnel indépendant.\n\n" +
            "Le présent contrat ne crée pas de relation employeur-employé, de société, de mandat général ou de représentation entre les Parties.\n\n" +
            "L’Enseignant demeure responsable de ses déclarations fiscales, cotisations sociales, assurances professionnelles, permis et autres obligations liées à son activité.\n\n" +
            "L’Enseignant ne peut engager juridiquement ou financièrement le Groupe ou TutorSphere sans autorisation écrite préalable."),

        new("art3", "Article 3 — Qualifications et informations fournies",
            "L’Enseignant certifie que les renseignements et documents transmis sont exacts, complets et authentiques, notamment : les pièces d’identité ; les diplômes et attestations ; les références professionnelles ; les autorisations d’enseigner, lorsqu’elles sont exigées ; les vérifications d’antécédents judiciaires ou de travail auprès de personnes vulnérables, lorsqu’elles sont requises.\n\n" +
            "L’Enseignant doit informer immédiatement le Groupe de tout changement pouvant affecter son aptitude à fournir les services.\n\n" +
            "Toute fausse déclaration ou utilisation de documents falsifiés peut entraîner la suspension immédiate du compte et la résiliation du contrat."),

        new("art4", "Article 4 — Obligations de l’enseignant",
            "L’Enseignant s’engage à :\n1. fournir des services professionnels, respectueux et adaptés au niveau de chaque élève ;\n2. respecter les horaires et les engagements acceptés ;\n3. préparer convenablement ses cours ;\n4. maintenir à jour ses disponibilités dans TutorSphere ;\n5. renseigner les présences, devoirs, observations et rapports demandés ;\n6. respecter les programmes scolaires, les objectifs pédagogiques et les consignes du Groupe ;\n7. utiliser un langage approprié et adopter un comportement exemplaire ;\n8. maintenir un environnement d’apprentissage sécuritaire ;\n9. préserver la confidentialité des renseignements concernant les élèves et leurs familles ;\n10. respecter les règles d’utilisation de TutorSphere ;\n11. signaler rapidement tout incident technique, pédagogique ou comportemental ;\n12. ne pas déléguer ses cours à une autre personne sans autorisation écrite."),

        new("art5", "Article 5 — Protection des enfants",
            "L’Enseignant reconnaît que certains services concernent des personnes mineures. Il s’engage à respecter les règles de protection des enfants ainsi que les lois applicables.\n\n" +
            "Il lui est notamment interdit : d’adopter un comportement violent, humiliant, discriminatoire ou intimidant ; d’avoir des échanges à caractère sexuel, romantique ou inapproprié avec un élève ; de demander des images, vidéos ou renseignements personnels sans justification pédagogique ; de rencontrer physiquement un élève sans l’autorisation préalable de son parent ou responsable légal ; d’organiser des communications privées en dehors des canaux autorisés ; de solliciter ou d’accepter des cadeaux, paiements ou avantages non autorisés.\n\n" +
            "Toute situation pouvant compromettre la sécurité ou le bien-être d’un enfant doit être signalée immédiatement au Groupe."),

        new("art6", "Article 6 — Utilisation de TutorSphere",
            "L’Enseignant doit utiliser son propre compte TutorSphere et protéger ses identifiants de connexion. Il est responsable des actions réalisées depuis son compte, sauf s’il démontre que celui-ci a été compromis après avoir pris des mesures de sécurité raisonnables.\n\n" +
            "L’Enseignant ne doit pas : partager son compte ; contourner les dispositifs de sécurité ; extraire ou utiliser les données de la plateforme sans autorisation ; perturber le fonctionnement du service ; utiliser la plateforme à des fins illégales ou étrangères à la mission pédagogique.\n\n" +
            "TutorSphere peut conserver les données techniques et les journaux d’activité nécessaires à la sécurité, à la conformité, à la facturation et au traitement des différends."),

        new("art7", "Article 7 — Organisation des cours",
            "Les cours peuvent être organisés en ligne ou en présentiel, selon l’offre acceptée par les Parties. Les horaires, matières, niveaux, durées et modalités sont définis dans TutorSphere ou dans une annexe au présent contrat.\n\n" +
            "L’Enseignant doit prévenir le Groupe et les personnes concernées en cas d’indisponibilité.\n\n" +
            "**Délai minimal d’annulation :** {{CANCELLATION_NOTICE}}\n**Tolérance de retard :** {{LATE_TOLERANCE}}\n**Conséquence d’une absence injustifiée :** {{UNJUSTIFIED_ABSENCE_RULE}}"),

        new("art8", "Article 8 — Rémunération",
            "En contrepartie des services effectivement fournis et validés, l’Enseignant reçoit la rémunération suivante :\n\n" +
            "**Taux ou formule de rémunération :** {{COMPENSATION_RATE}}\n**Devise :** {{CURRENCY}}\n**Fréquence de paiement :** {{PAYMENT_FREQUENCY}}\n" +
            "**Moyen de paiement :** {{PAYMENT_METHOD}}\n**Commission ou frais applicables :** {{APPLICABLE_FEES}}\n\n" +
            "Le paiement peut être conditionné à la validation des présences, des rapports de cours et des autres éléments exigés. Les taxes, impôts, frais bancaires et obligations fiscales personnelles de l’Enseignant demeurent à sa charge, sauf disposition légale contraire.\n\n" +
            "Toute contestation relative à un paiement doit être présentée dans un délai de {{PAYMENT_DISPUTE_PERIOD}} jours."),

        new("art9", "Article 9 — Relation avec les élèves et les parents",
            "L’Enseignant doit utiliser les moyens de communication autorisés par le Groupe.\n\n" +
            "Pendant la durée du contrat et durant {{NON_SOLICITATION_PERIOD}} après sa cessation, l’Enseignant s’engage, dans les limites autorisées par la loi applicable, à ne pas contourner la plateforme pour proposer directement des services payants aux élèves ou familles qui lui ont été présentés par le Groupe.\n\n" +
            "Cette clause ne s’applique pas aux relations dont l’Enseignant peut démontrer qu’elles existaient avant son inscription auprès du Groupe."),

        new("art10", "Article 10 — Confidentialité",
            "Sont considérées comme confidentielles toutes les informations non publiques relatives aux élèves et à leurs familles, aux enseignants et membres du Groupe, aux méthodes pédagogiques, aux tarifs et modalités commerciales, aux données financières, aux systèmes techniques, ainsi qu’aux documents internes et stratégies du Groupe.\n\n" +
            "L’Enseignant ne peut divulguer ou utiliser ces informations en dehors de l’exécution du présent contrat. Cette obligation demeure applicable après la fin du contrat."),

        new("art11", "Article 11 — Protection des données personnelles",
            "L’Enseignant s’engage à traiter uniquement les renseignements personnels nécessaires à l’exécution de ses services. Il doit notamment : conserver les données de manière sécurisée ; ne pas copier inutilement les renseignements personnels ; ne pas transmettre les données à un tiers non autorisé ; supprimer ou restituer les données à la fin de sa mission ; signaler immédiatement toute perte, divulgation ou utilisation non autorisée.\n\n" +
            "Les données doivent être traitées conformément aux lois applicables en matière de protection de la vie privée."),

        new("art12", "Article 12 — Enregistrement des cours et utilisation de l’IA",
            "Un cours ne peut être enregistré, retranscrit ou analysé par un système d’intelligence artificielle que si cette fonctionnalité a été autorisée par le Groupe et que les consentements légalement requis ont été obtenus.\n\n" +
            "L’Enseignant est informé avant l’activation d’un enregistrement ou d’une analyse automatisée. Les enregistrements, résumés et transcriptions ne doivent être utilisés qu’aux fins autorisées : suivi pédagogique, contrôle de qualité, sécurité, compte rendu ou amélioration du service."),

        new("art13", "Article 13 — Propriété intellectuelle",
            "L’Enseignant conserve la propriété des contenus pédagogiques qu’il avait créés avant le présent contrat. Il accorde au Groupe une autorisation non exclusive d’utiliser les contenus qu’il dépose sur TutorSphere dans la mesure nécessaire à la fourniture, au suivi et à l’archivage des services concernés.\n\n" +
            "Les contenus créés spécifiquement et rémunérés par le Groupe appartiennent à : {{INTELLECTUAL_PROPERTY_RULE}}.\n\n" +
            "L’Enseignant garantit qu’il possède les droits nécessaires sur les documents utilisés et qu’il respecte les droits d’auteur."),

        new("art14", "Article 14 — Évaluation et contrôle de qualité",
            "Le Groupe peut évaluer la qualité des services à partir des rapports de cours, du respect des horaires, des commentaires des parents et élèves, des résultats pédagogiques, des incidents signalés et des contrôles de conformité autorisés.\n\n" +
            "L’Enseignant peut consulter les évaluations le concernant et transmettre ses observations. En cas de difficulté, le Groupe peut demander une formation, un plan d’amélioration ou prendre une mesure de suspension."),

        new("art15", "Article 15 — Durée du contrat",
            "Le présent contrat est conclu : {{CONTRACT_DURATION_TYPE}}.\n\n" +
            "Il entre en vigueur à la date de sa signature électronique par les Parties.\n\n" +
            "S’il est à durée déterminée, il prend fin le {{CONTRACT_END_DATE}}, sauf renouvellement ou résiliation anticipée."),

        new("art16", "Article 16 — Suspension et résiliation",
            "Chaque Partie peut résilier le contrat en respectant un préavis de {{TERMINATION_NOTICE}} jours.\n\n" +
            "Le Groupe peut suspendre immédiatement l’Enseignant en cas de risque pour la sécurité d’un élève, de fraude ou de fausse déclaration, de violation grave de la confidentialité, de comportement abusif ou discriminatoire, de violation répétée des horaires ou obligations pédagogiques, d’utilisation non autorisée des données, ou de non-respect grave du présent contrat ou de la loi.\n\n" +
            "Avant une résiliation définitive, l’Enseignant doit normalement être informé des motifs et avoir la possibilité de présenter ses observations, sauf urgence liée à la sécurité ou obligation légale."),

        new("art17", "Article 17 — Effets de la fin du contrat",
            "À la fin du contrat, l’Enseignant doit : terminer ou transmettre les rapports encore dus ; restituer ou supprimer les données confidentielles ; cesser d’utiliser les ressources et accès du Groupe ; respecter les obligations qui demeurent applicables après la fin du contrat.\n\n" +
            "Les montants dus pour les services valablement fournis restent payables conformément aux modalités prévues."),

        new("art18", "Article 18 — Responsabilité",
            "Chaque Partie est responsable de ses propres actes, erreurs, omissions et violations de la loi. L’Enseignant est responsable de la qualité professionnelle de ses services et des dommages résultant d’une faute, d’une négligence grave ou d’un comportement intentionnel.\n\n" +
            "TutorSphere constitue un outil technologique de gestion et de communication. La plateforme ne remplace pas la responsabilité pédagogique du Groupe ou de l’Enseignant.\n\n" +
            "Aucune disposition du présent contrat ne limite une responsabilité qui ne peut légalement être exclue."),

        new("art19", "Article 19 — Force majeure",
            "Aucune Partie n’est responsable d’un retard causé par un événement raisonnablement indépendant de sa volonté, notamment une catastrophe, une panne majeure, un conflit, une décision gouvernementale ou une interruption généralisée des communications.\n\n" +
            "La Partie concernée doit informer l’autre Partie dès que possible et prendre des mesures raisonnables pour réduire les conséquences."),

        new("art20", "Article 20 — Règlement des différends",
            "Les Parties s’engagent d’abord à rechercher une solution amiable. Toute réclamation doit être transmise par écrit et contenir les faits, les éléments justificatifs et la solution demandée.\n\n" +
            "À défaut d’accord dans un délai de {{AMICABLE_RESOLUTION_PERIOD}} jours, le différend sera soumis au mécanisme suivant : {{DISPUTE_RESOLUTION_METHOD}}."),

        new("art21", "Article 21 — Droit applicable",
            "Le présent contrat est régi par les lois de : {{GOVERNING_JURISDICTION}}.\n\n" +
            "Les tribunaux ou organismes compétents sont ceux de : {{COMPETENT_COURT_LOCATION}}.\n\n" +
            "Cette clause s’applique sous réserve des règles impératives du pays de résidence de l’Enseignant ou du lieu d’exécution des services."),

        new("art22", "Article 22 — Modification du contrat",
            "Toute modification importante doit être communiquée à l’Enseignant. Lorsqu’une nouvelle version nécessite son consentement, un nouveau contrat ou un avenant doit lui être envoyé pour signature.\n\n" +
            "La nouvelle version ne remplace la précédente qu’après son acceptation, sauf lorsque la loi autorise une autre procédure."),

        new("art23", "Article 23 — Intégralité et divisibilité",
            "Le présent contrat et ses annexes représentent l’intégralité de l’accord entre les Parties concernant son objet.\n\n" +
            "Si une disposition est déclarée invalide, les autres dispositions demeurent applicables, dans la mesure permise par la loi."),

        new("art24", "Article 24 — Signature électronique",
            "L’Enseignant reconnaît avoir : reçu le contrat dans une langue qu’il comprend ; pu consulter l’intégralité du document ; vérifié l’exactitude des informations le concernant ; accepté chacune des conditions obligatoires ; signé le contrat librement par voie électronique ; reçu ou obtenu l’accès à une copie du contrat signé.\n\n" +
            "Après signature, le système génère un document PDF définitif contenant notamment le numéro unique du contrat, la version et la langue, la date et l’heure de la signature, l’identité des signataires, l’empreinte numérique du fichier, un code QR de vérification et les éléments de preuve consignés dans le journal d’audit."),

        new("annexes", "Annexes",
            "Font partie intégrante du présent contrat, telles qu’elles figurent dans TutorSphere :\n\n" +
            "• Annexe A — Matières et niveaux enseignés\n• Annexe B — Disponibilités et horaires\n• Annexe C — Rémunération et modalités de paiement\n" +
            "• Annexe D — Politique d’annulation et d’absence\n• Annexe E — Code de conduite et protection des enfants\n" +
            "• Annexe F — Politique de confidentialité et de protection des données\n• Annexe G — Règles d’utilisation de TutorSphere")
    ];
}
