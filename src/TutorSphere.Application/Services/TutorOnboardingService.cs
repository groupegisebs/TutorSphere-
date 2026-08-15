using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.DTOs.Onboarding;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Application.Services;

public interface ITutorOnboardingService
{
    Task<TutorOnboardingStatusDto> GetStatusAsync(string ownerUserId, string culture, CancellationToken ct = default);
    Task<CompleteOnboardingModuleResult> CompleteModuleAsync(
        string ownerUserId,
        CompleteOnboardingModuleRequest request,
        string culture,
        CancellationToken ct = default);
}

public class TutorOnboardingService(
    IApplicationDbContext db,
    IEmailService email,
    IUserContactLookup contacts,
    IAppUrlProvider urls) : ITutorOnboardingService
{
    public Task<TutorOnboardingStatusDto> GetStatusAsync(string ownerUserId, string culture, CancellationToken ct = default)
    {
        var tenant = RequireOwner(ownerUserId);
        var completedIds = ParseCompletedModules(tenant);
        var modules = BuildModulesForClient(culture, completedIds);
        return Task.FromResult(ToStatus(tenant, modules));
    }

    public async Task<CompleteOnboardingModuleResult> CompleteModuleAsync(
        string ownerUserId,
        CompleteOnboardingModuleRequest request,
        string culture,
        CancellationToken ct = default)
    {
        var tenant = RequireOwner(ownerUserId);
        if (!tenant.HasPaidLicense())
            return new CompleteOnboardingModuleResult(request.ModuleId, false, false, false,
                "La licence annuelle doit être payée avant la formation.");

        if (tenant.OnboardingCompletedAt is not null)
            return new CompleteOnboardingModuleResult(request.ModuleId, true, true, tenant.HasValidLicense(), null);

        var completedIds = ParseCompletedModules(tenant);
        var catalog = GetModuleCatalog(culture);
        var module = catalog.FirstOrDefault(m => m.Id.Equals(request.ModuleId, StringComparison.OrdinalIgnoreCase));
        if (module is null)
            return new CompleteOnboardingModuleResult(request.ModuleId, false, false, false, "Module introuvable.");

        var previous = catalog.Where(m => m.Order < module.Order).ToList();
        if (previous.Any(m => !completedIds.Contains(m.Id, StringComparer.OrdinalIgnoreCase)))
            return new CompleteOnboardingModuleResult(request.ModuleId, false, false, false,
                "Complétez les modules précédents d'abord.");

        if (module.Quiz.Count > 0)
        {
            if (request.QuizAnswers is null || request.QuizAnswers.Count != module.Quiz.Count)
                return new CompleteOnboardingModuleResult(request.ModuleId, false, false, false,
                    "Répondez à toutes les questions du quiz.");

            for (var i = 0; i < module.Quiz.Count; i++)
            {
                if (request.QuizAnswers[i] != module.Quiz[i].CorrectIndex)
                    return new CompleteOnboardingModuleResult(request.ModuleId, false, false, false,
                        "Certaines réponses sont incorrectes. Relisez le module et réessayez.");
            }
        }

        if (!completedIds.Contains(module.Id, StringComparer.OrdinalIgnoreCase))
            completedIds.Add(module.Id);

        SetCompletedModules(tenant, completedIds);

        var allDone = catalog.All(m => completedIds.Contains(m.Id, StringComparer.OrdinalIgnoreCase));
        if (allDone)
        {
            tenant.OnboardingCompletedAt = DateTime.UtcNow;
            tenant.Status = TenantStatus.Active;
            // Profil public seulement après validation par un groupe d'experts.
            tenant.IsPublicProfile = tenant.ExpertApprovalStatus == ExpertApprovalStatus.Approved;
            tenant.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var contact = await contacts.GetAsync(tenant.OwnerUserId, ct);
            if (contact is { } c && !string.IsNullOrWhiteSpace(c.Email))
            {
                var firstName = c.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                                ?? c.DisplayName;
                // E-mail « école active » uniquement si déjà validée par un expert (sinon reste privée).
                if (tenant.IsPublicProfile)
                {
                    await email.SendSchoolApprovedAsync(
                        c.Email,
                        firstName,
                        tenant.Name,
                        $"{urls.WebBaseUrl.TrimEnd('/')}/login/tuteur",
                        ct);
                }
            }

            return new CompleteOnboardingModuleResult(module.Id, true, true, tenant.HasValidLicense() && tenant.IsPublicProfile, null);
        }

        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new CompleteOnboardingModuleResult(module.Id, true, false, false, null);
    }

    private Domain.Entities.Tenant RequireOwner(string ownerUserId) =>
        db.Tenants.FirstOrDefault(t => t.OwnerUserId == ownerUserId)
            ?? throw new InvalidOperationException("Aucun profil enseignant associé à ce compte.");

    private static TutorOnboardingStatusDto ToStatus(
        Domain.Entities.Tenant tenant,
        IReadOnlyList<TutorOnboardingModuleDto> modules) =>
        new(
            tenant.Id,
            tenant.Name,
            tenant.HasPaidLicense(),
            tenant.RequiresOnboarding(),
            tenant.HasValidLicense(),
            tenant.OnboardingCompletedAt,
            tenant.LicenseExpiresAt,
            modules,
            modules.Count(m => m.IsCompleted),
            modules.Count);

    private static List<string> ParseCompletedModules(Domain.Entities.Tenant tenant)
    {
        var raw = tenant.OnboardingProgress;
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void SetCompletedModules(Domain.Entities.Tenant tenant, IReadOnlyList<string> ids) =>
        tenant.OnboardingProgress = string.Join(',', ids);

    private static IReadOnlyList<TutorOnboardingModuleDto> BuildModulesForClient(
        string culture,
        IReadOnlyList<string> completed) =>
        GetModuleCatalog(culture)
            .Select(d => new TutorOnboardingModuleDto(
                d.Id,
                d.Order,
                d.Title,
                d.Summary,
                d.BodyHtml,
                d.Quiz.Select(q => new TutorOnboardingQuizItemDto(q.Question, q.Choices)).ToList(),
                completed.Contains(d.Id, StringComparer.OrdinalIgnoreCase),
                d.VideoUrl))
            .ToList();

    private static IReadOnlyList<ModuleDef> GetModuleCatalog(string culture) =>
        culture.StartsWith("fr", StringComparison.OrdinalIgnoreCase) ? FrModules() : EnModules();

    /// <summary>Vidéo démo « créer une offre » (fichier Web wwwroot ou URL YouTube/Vimeo).</summary>
    public const string CreateOfferGuideVideoUrl = "/videos/guides/create-offer.mp4";

    private sealed record QuizDef(string Question, IReadOnlyList<string> Choices, int CorrectIndex);

    private sealed record ModuleDef(
        string Id,
        int Order,
        string Title,
        string Summary,
        string BodyHtml,
        IReadOnlyList<QuizDef> Quiz,
        string? VideoUrl = null);

    private static List<ModuleDef> FrModules() =>
    [
        new("welcome", 1, "Bienvenue sur TutorSphere",
            "Votre profil enseignant en 2 minutes.",
            """
            <p>TutorSphere est votre <strong>espace répétiteur</strong> : élèves, cours, devoirs et paiements parents.</p>
            <p>Après cette formation, votre profil sera <strong>actif et visible</strong> dans la recherche parents.</p>
            <p>Rappel : vous avez accepté le <strong>Code de conduite et d’éthique enseignant</strong> (respect, sécurité des mineurs, signalement, confidentialité). Un manquement peut entraîner la suspension du compte.</p>
            <ul><li>Tableau de bord</li><li>Offres d'abonnement</li><li>Calendrier et salle de classe</li></ul>
            """,
            [new("Quel est l'objectif de TutorSphere pour vous ?",
                ["Réserver un hôtel", "Gérer votre profil d'enseignant", "Acheter des fournitures"], 1)]),
        new("offers", 2, "Créer vos offres de cours",
            "Regardez la vidéo puis publiez une offre que les parents peuvent choisir.",
            """
            <p>Regardez la <strong>vidéo de démonstration</strong> ci-dessous : elle montre comment créer une offre étape par étape.</p>
            <p>Menu <strong>Offres</strong> → <strong>Nouvelle offre</strong> : matière, cycle, mode (en ligne / présentiel), puis tarification.</p>
            <p>Modes de facturation : <strong>taux horaire</strong>, <strong>taux par séance</strong>, ou <strong>abonnement trimestriel</strong> — chaque cours n’est comptabilisé qu’après validation de son effectivité.</p>
            <p>Une offre active devient visible aux parents <em>une fois votre profil public</em>.</p>
            """,
            [new("Quand un cours est-il facturé ?",
                ["Dès la réservation", "Après validation / confirmation de l'effectivité du cours", "Une fois par an seulement"], 1),
             new("Où créez-vous une offre de cours ?",
                ["Menu Offres", "Menu Messages", "Espace parent"], 0)],
            CreateOfferGuideVideoUrl),
        new("students", 3, "Élèves, parents et inscriptions",
            "Accepter une demande puis encaisser le paiement.",
            """
            <p>Un parent (ou élève 14+) s'inscrit à une offre → vous <strong>acceptez</strong> la demande.</p>
            <p>Ensuite le parent paie via la passerelle sécurisée ; l'abonnement passe <strong>Actif</strong>.</p>
            <p>Gérez vos élèves dans <strong>Mes élèves</strong> et les parents dans <strong>Parents</strong>.</p>
            """,
            [new("Quand le parent peut-il payer ?",
                ["Avant toute inscription", "Après votre acceptation de la demande", "Jamais"], 1)]),
        new("calendar", 4, "Calendrier, cours et devoirs",
            "Planifier et animer vos séances.",
            """
            <p><strong>Calendrier / Cours</strong> : les séances peuvent être générées après paiement.</p>
            <p>Lancez la <strong>salle de classe</strong> le jour J, suivez les présences, publiez des <strong>devoirs</strong> et rapports.</p>
            <p>Indiquez vos indisponibilités pour éviter les conflits.</p>
            """,
            [new("Que pouvez-vous faire le jour d'un cours ?",
                ["Ouvrir la salle de classe et suivre les présences", "Modifier le prix Stripe manuellement", "Supprimer la plateforme"], 0)]),
        new("payouts", 5, "Revenus et visibilité",
            "Paiements, commission et profil public.",
            """
            <p>Les parents paient vos forfaits ; TutorSphere prélève une <strong>commission</strong> (5–15 %).</p>
            <p>Configurez un compte de versement (Stripe Connect / PayPal) dans les paramètres de payout.</p>
            <p>À la fin de cette formation, votre profil devient <strong>visible par tous</strong> dans la recherche.</p>
            """,
            [new("Quand votre profil devient-il visible publiquement ?",
                ["Dès l'inscription", "Après paiement + cette formation", "Jamais"], 1)])
    ];

    private static List<ModuleDef> EnModules() =>
    [
        new("welcome", 1, "Welcome to TutorSphere",
            "Your digital tutoring school in 2 minutes.",
            """
            <p>TutorSphere is your <strong>tutor workspace</strong>: students, lessons, homework and parent payments.</p>
            <p>After this training, your school will be <strong>active and visible</strong> in parent search.</p>
            <p>Reminder: you accepted the <strong>Teacher Code of Conduct and Ethics</strong> (respect, child safety, reporting, confidentiality). A breach may lead to account suspension.</p>
            """,
            [new("What is TutorSphere for you?",
                ["Book a hotel", "Run your tutoring school", "Buy supplies"], 1)]),
        new("offers", 2, "Create course offerings",
            "Watch the demo video, then publish an offer parents can enroll in.",
            """
            <p>Watch the <strong>demo video</strong> below: it shows how to create an offer step by step.</p>
            <p>Go to <strong>Offers</strong> → <strong>New offer</strong>: subject, cycle, mode (online / in-person), then pricing.</p>
            <p>Billing modes: <strong>hourly rate</strong>, <strong>per-session rate</strong>, or <strong>quarterly subscription</strong> — each lesson is charged only after it is validated as having taken place.</p>
            <p>Active offers become visible once your school is public.</p>
            """,
            [new("When is a lesson billed?",
                ["As soon as it is booked", "After the lesson is validated / confirmed as completed", "Once a year only"], 1),
             new("Where do you create a course offer?",
                ["Offers menu", "Messages menu", "Parent portal"], 0)],
            CreateOfferGuideVideoUrl),
        new("students", 3, "Students, parents and enrollment",
            "Accept a request, then collect payment.",
            """
            <p>A parent enrolls → you <strong>accept</strong> → they pay → subscription becomes <strong>Active</strong>.</p>
            """,
            [new("When can the parent pay?",
                ["Before enrollment", "After you accept the request", "Never"], 1)]),
        new("calendar", 4, "Calendar, lessons and homework",
            "Plan and run sessions.",
            """
            <p>Open the <strong>classroom</strong>, track attendance, publish <strong>homework</strong> and reports.</p>
            """,
            [new("What can you do on lesson day?",
                ["Open the classroom and track attendance", "Edit Stripe prices manually", "Delete the platform"], 0)]),
        new("payouts", 5, "Revenue and visibility",
            "Payouts, commission and public profile.",
            """
            <p>Parents pay your packages; TutorSphere takes a <strong>commission</strong> (5–15%).</p>
            <p>Completing this training makes your school <strong>visible to everyone</strong>.</p>
            """,
            [new("When does your school become publicly visible?",
                ["Right after signup", "After payment + this training", "Never"], 1)])
    ];
}
