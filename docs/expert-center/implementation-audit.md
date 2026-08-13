# Audit — Centre de gouvernance Expert (TutorSphere)

**Date :** 2026-08-13  
**Périmètre :** `TutorSphere-`  
**Règle :** une page `/expert/coming-soon/*` n’est **jamais** une fonctionnalité.  
**Statut de ce document :** audit seulement — **aucune implémentation de module** dans cette étape.

---

## 1. Synthèse exécutive

| Constat | Détail |
|--------|--------|
| Menus prématurés | **18** entrées sidebar pointent vers `ComingSoon.razor` tout en étant visibles |
| Feature flags | **Absents** (aucun `IFeatureManager` / FeatureManagement) |
| Routes `group-admin/*` | **Absentes** (0 fichier) |
| Tests Expert | **Absents** (seul `UserRolesTests` mentionne la chaîne `"Expert"`) |
| Journal d’audit Expert | **Absent** (pas d’entité `ExpertAudit` / event store) |
| Lot 1 (cible) | Partiellement présent sous d’autres routes / modèles — **écarts de contrat produit** |
| Placeholders | Ne doivent plus être présentés comme modules disponibles |

**Verdict global :** le centre Expert a un **noyau partiel** (file enseignants, disciplines, admissions vote, mandat Responsable, chat/offres fins) mais le **menu promet des modules non construits**. Les critères « Completed » du lot 1 ne sont **pas** atteints.

---

## 2. Inventaire des routes `coming-soon`

**Page unique :** `src/TutorSphere.Web/Components/Pages/Expert/ComingSoon.razor`  
**Route :** `/expert/coming-soon/{Feature}`  
**Contenu :** message statique « Module en cours de déploiement… » — **PlaceholderOnly**.

| Feature (URL) | Label menu | Section sidebar |
|---------------|------------|-----------------|
| `interviews` | Entretiens | Enseignants |
| `demonstrations` | Démonstrations | Enseignants |
| `documents` | Documents | Enseignants |
| `renewals` | Réévaluations | Enseignants |
| `quality` | Suivi qualité | Qualité |
| `observations` | Observations | Qualité |
| `resources` | Supports | Qualité |
| `feedback` | Remarques | Qualité |
| `incidents` | Incidents | Qualité |
| `library` | Bibliothèque | Organisation |
| `training` | Formations | Organisation |
| `visibility` | Visibilité | Organisation |
| `meetings` | Réunions | Groupe |
| `decisions` | Décisions | Groupe |
| `reports` | Rapports | Admin / Outils |
| `activity` | Journal d'activité | Administration (manager) |
| `notifications` | Notifications | Outils |
| `profile` | Mon profil | Outils |

**Fichier menu :** `src/TutorSphere.Web/Components/Layout/ExpertSidebar.razor`

---

## 3. Menu Expert — liens réels vs placeholders

### Liens réels (UI non-placeholder)

| Route | Page |
|-------|------|
| `/expert/dashboard` | `Dashboard.razor` |
| `/expert/approvals` | `Approvals.razor` |
| `/expert/teachers` | `Teachers.razor` |
| `/expert/teachers/{id}` | `TeacherReview.razor` |
| `/expert/disciplines` | `Disciplines.razor` |
| `/expert/offers` | `Offers.razor` (mince) |
| `/expert/members` | `Members.razor` |
| `/expert/admissions` | `Admissions.razor` |
| `/expert/join` | `Join.razor` |
| `/expert/admin-chat` | `AdminChat.razor` (Responsable) |

### Liens placeholder

Tous les `expert/coming-soon/*` listés §2.

### Espace Admin lié

| Route | Page | État |
|-------|------|------|
| `/admin/expert-groups` | `ExpertGroups.razor` | Partiel (Responsable) |
| `/admin/expert-groups/{id}` | `ExpertGroupMembers.razor` | Partiel |
| `/admin/expert-groups/messages` | `ExpertGroupMessages.razor` | Partiel (chat) |
| `/admin/expert-groups/{id}/manager` | — | **NotStarted** |
| `/group-admin/*` | — | **NotStarted** |

---

## 4. Tableau d’état par module

Légende des états : `NotStarted` | `PlaceholderOnly` | `PartiallyImplemented` | `ImplementedNotTested` | `Completed`

| Module | Menu | Entité | API | UI réelle | Permissions | Tests | État |
|--------|------|--------|-----|-----------|-------------|-------|------|
| Tableau de bord Expert | Oui (réel) | Agrégats | `GET api/expert/dashboard-summary` | Oui | Expert | Non | ImplementedNotTested |
| File d’attente enseignants | Oui (réel) | Tenant + champs revue | queue / approve / reject / request-changes / assign / start-review | Oui (`Approvals`, `TeacherReview`) | Expert | Non | PartiallyImplemented |
| Affectation dossiers (Responsable) | Non dédié | `ReviewAssignedToUserId`, priorité | `POST .../assign` | **API oui / UI assign absente** | Expert (pas de distinction Manager côté UI) | Non | PartiallyImplemented |
| Enseignants approuvés + remarques | Oui (réel) | `ExpertRemark` | remarks CRUD | Oui (`Teachers`) | Expert / Tutor | Non | PartiallyImplemented |
| Entretiens | Oui → coming-soon | Non | Non | Non | — | Non | PlaceholderOnly |
| Démonstrations | Oui → coming-soon | Non | Non | Non | — | Non | PlaceholderOnly |
| Documents & échéances (Expert) | Oui → coming-soon | `TeacherDocument` (tutor/expert review) | docs review partiels | Non (menu → placeholder) | — | Non | PlaceholderOnly |
| Réévaluations | Oui → coming-soon | Non | Non | Non | — | Non | PlaceholderOnly |
| Suivi qualité | Oui → coming-soon | Non dédié | Non | Non | — | Non | PlaceholderOnly |
| Observations de cours | Oui → coming-soon | Non | Non | Non | — | Non | PlaceholderOnly |
| Supports pédagogiques | Oui → coming-soon | Non | Non | Non | — | Non | PlaceholderOnly |
| Remarques & plans (menu Qualité) | Oui → coming-soon | `ExpertRemark` existe ailleurs | remarks sur enseignants | Menu Qualité = placeholder | Expert | Non | PlaceholderOnly |
| Incidents / signalements | Oui → coming-soon | Non | Non | Non | — | Non | PlaceholderOnly |
| Disciplines | Oui (réel) | `Discipline`, assignments | `api/expert/disciplines` | Oui | Expert | Non | ImplementedNotTested |
| Offres du groupe | Oui (réel) | `GroupOffer`, `GroupOfferTeacher` | list/create/publish | UI mince ; **affectation enseignants non branchée** | Publish = Manager | Non | PartiallyImplemented |
| Bibliothèque | Oui → coming-soon | Non | Non | Non | — | Non | PlaceholderOnly |
| Formations | Oui → coming-soon | Non | Non | Non | — | Non | PlaceholderOnly |
| Visibilité géographique | Oui → coming-soon | Champs tenant/pays | Non module | Non | — | Non | PlaceholderOnly |
| Membres du groupe | Oui (réel) | `ExpertGroupMember` | `api/expert/membership/members` | Oui | Expert | Non | PartiallyImplemented |
| Admissions Experts (vote 75 %) | Oui (réel) | Invite + Vote | `api/expert/membership/*` + admin | Oui (`Admissions`, `Join`) | Expert invite/vote ; admin force | Non | PartiallyImplemented |
| Réunions | Oui → coming-soon | Non | Non | Non | — | Non | PlaceholderOnly |
| Décisions | Oui → coming-soon | Non | Non | Non | — | Non | PlaceholderOnly |
| Chat Responsable–Admin | Oui si Manager | Conversation + Message | `api/expert/admin-chat`, `api/admin/.../messages` | Oui | Manager / Admin | Non | PartiallyImplemented |
| Rapports | Oui → coming-soon | Non | Non | Non | — | Non | PlaceholderOnly |
| Journal d’activité / audit | Oui → coming-soon | Non | Non | Non | — | Non | PlaceholderOnly |
| Notifications (module) | Oui → coming-soon | Non store | e-mails ponctuels | Non | — | Non | PlaceholderOnly |
| Mon profil Expert | Oui → coming-soon | Identity | Non | Non | — | Non | PlaceholderOnly |
| Responsable de groupe (mandat) | Admin liste | `ExpertGroupManagerMandate` ; MemberRole | appoint / suspend | Admin partiel ; **pas** `/admin/.../manager` ni `/group-admin/*` | Admin appoint | Non | PartiallyImplemented |
| Feature flags par module | Non | Non | Non | Non | — | Non | NotStarted |
| Demande d’intérêt enseignant | Lien public | `TeacherInterestRequest` | `POST api/public/teacher-interest` | `/tutor/interest` | Public | Non | ImplementedNotTested |
| Inscription enseignant sans invite | — | — | `RegisterSchool` exige `InviteToken` | `/tutor/register?invite=` | — | Non | ImplementedNotTested |
| Ajout direct Expert | Admin membres | Member + AdminDirect | `api/admin/expert-groups/.../members` | Admin UI | **SuperAdmin + PlatformAdmin** (pas Expert) | Non | PartiallyImplemented |

---

## 5. Détail Lot 1 — écarts vs spécification

### A. Responsable du groupe

| Exigence | État actuel |
|----------|-------------|
| `GroupManagerMembershipId` | **Manquant** — présent : `ActiveManagerMandateId` |
| `ManagerAssignedAtUtc` / `ManagerAssignedByAdminId` sur groupe | **Manquants** — présents sur `ExpertGroupManagerMandate` (`MandateStartsAtUtc`, `AppointedByAdminId`) |
| `MemberRole` / Status / AdmissionMethod | **Présents** sur `ExpertGroupMember` |
| Un seul Responsable actif | **Oui** (service mandat) |
| Pages `/admin/expert-groups/{id}/manager` | **Non** |
| `/group-admin/dashboard\|settings\|members` | **Non** (équivalent partiel sous `/expert/*`) |
| Rôle Identity `GroupManager` | **Non** — flag métier via mandat + `me-context` |

### B. Admissions Experts

| Exigence | État actuel |
|----------|-------------|
| Invitation / candidature / votes | **Oui** |
| Liste figée votants | **Oui** (`EligibleVoterUserIdsCsv` à l’ouverture) |
| `Ceiling(eligible × 0.75)` | **Oui** (`IExpertMembershipGovernanceService.RequiredApprovals`) |
| Initiateur exclu | **Oui** (construction eligible) |
| Routes `/expert/members/admissions[...]` | **Non** — routes actuelles : `/expert/admissions`, `/expert/join` |
| Notifications | **Partielles** (e-mail) |
| Audit | **Non** |

### C. Inscription enseignants

| Chemin | État |
|--------|------|
| Invitation Expert | **Oui** |
| Création directe Expert | **Oui** |
| Demande d’intérêt | **Oui** (`/tutor/interest`) |
| Auto-inscription sans token | **Bloquée** côté `RegisterSchoolAsync` |

### D. Affectation des dossiers

| Exigence | État |
|----------|------|
| Champs assign / priorité | **Oui** sur Tenant |
| API assign | **Oui** |
| UI Responsable (non attribués, réaffecter, délais) | **Non** |
| Filtrage Expert « mes dossiers » vs groupe | **Partiel** |

### E. Audit & notifications

| Exigence | État |
|----------|------|
| Événements structurés (invitation, vote, nomination, etc.) | **NotStarted** |
| Notifications unifiées | **Partielles** (e-mails ad hoc) |

---

## 6. Inventaire technique (référence)

### Entités Domain (Expert / Group)

- `ExpertGroup`, `ExpertGroupMember`, `ExpertGroupManagerMandate`
- `ExpertMembershipInvite`, `ExpertMembershipVote`
- `ExpertRemark`
- `Discipline`, `DisciplineServiceItem`, `TeacherDisciplineAssignment`
- `GroupOffer`, `GroupOfferTeacher`
- `GroupAdminConversation`, `GroupAdminMessage`
- `TeacherApplicationInvite`, `TeacherInterestRequest`, `TeacherDocument`

### Services Application

`ExpertDashboardService`, `ExpertApprovalService`, `ExpertGroupService`, `ExpertGroupManagerService`, `ExpertMembershipGovernanceService`, `ExpertDisciplineService`, `ExpertMonitoringService`, `ExpertReviewNotificationService`, `GroupOfferService`, `GroupAdminChatService`, `TeacherInterestService`

### Migrations notables

- `AddExpertGroupTeacherApproval`, `AddExpertGroupContactName`
- `AddTeacherApplicationInvites`, `AddExpertRemarks`, `AddDisciplines`
- `AddExpertMembershipGovernance`
- `AddExpertReviewAssignmentFields`
- `AddGroupManagerGovernance` (mandat, offres, chat, interest)

### Tests

- `tests/TutorSphere.UnitTests/UserRolesTests.cs` uniquement  
- **Aucun** test admissions / mandat / assign / offres / chat

### Feature flags

- **Aucun** mécanisme projet

---

## 7. Plan du Lot 1 (après validation de cet audit)

Objectif : atteindre les **critères de fin du Lot 1** sans toucher Lot 2/3 ni modules Qualité placeholders.

### Phase 0 — Gouvernance menu & flags (prérequis)

1. Introduire feature flags par module (config + helper).
2. Masquer en Production les modules `PlaceholderOnly` / `NotStarted`.
3. Badge « Bientôt disponible » uniquement en Development si flag `ShowComingSoonBadges`.
4. Ne **pas** supprimer `ComingSoon.razor` tant qu’un flag pointe encore dessus ; retirer les NavLinks faux-positifs.

### Phase 1A — Responsable (contrat produit)

1. Aligner modèle : `GroupManagerMembershipId`, `ManagerAssignedAtUtc`, `ManagerAssignedByAdminId` (en plus ou à la place du dénormalisé actuel — migration additive).
2. Page admin dédiée `/admin/expert-groups/{id}/manager` (nommer / remplacer / suspendre / historique).
3. Espace `/group-admin/dashboard`, `/group-admin/settings`, `/group-admin/members` (réutiliser services ; autoriser Manager).
4. Vérifier serveur : seul SuperAdmin (décision produit : **restreindre** `PlatformAdmin` si exigé strictement) pour nomination.

### Phase 1B — Admissions

1. Alias / nouvelles routes `/expert/members/admissions[...]` (pages détail + vote).
2. Couvrir rejet mathématique, expiration, audit events.
3. Tests unitaires formule + freeze + CastVote refus hors liste.

### Phase 1C — Enseignants

1. Conserver les 3 chemins ; tests « register sans invite → 400 ».
2. File intérêt visible Responsable/Expert.

### Phase 1D — Affectation dossiers

1. UI file : non attribués / mes dossiers / réaffecter / priorité / âge.
2. Autorisations : Manager = répartition groupe ; Expert = ses dossiers + non attribués selon règle.

### Phase 1E — Audit + notifications

1. Entité `ExpertGovernanceEvent` (ou équivalent) + écriture sur actions listées.
2. Notifications (e-mail ou in-app) branchées sur les mêmes événements.
3. Page journal minimale (remplace coming-soon `activity` **seulement** quand réelle).

### Ordre d’exécution imposé

`Flags → Modèle Manager → Pages group-admin/admin manager → Admissions routes+tests → Assign UI+ACL → Audit events → Tests Lot 1 → Compilation`

**Interdiction :** ne pas démarrer Lot 2 (offres enrichies) ni Lot 3 (chat temps réel/pièces jointes) avant critères Lot 1.

---

## 8. Liste exacte des fichiers à modifier / créer (Lot 1)

### À créer

| Fichier |
|---------|
| `docs/expert-center/lot1-checklist.md` *(suivi critères fin)* |
| `src/TutorSphere.Domain/Entities/ExpertGovernanceEvent.cs` |
| `src/TutorSphere.Domain/Enums/ExpertGovernanceEventType.cs` |
| `src/TutorSphere.Application/Options/ExpertModuleFeatureOptions.cs` |
| `src/TutorSphere.Application/Services/IExpertGovernanceAuditService.cs` |
| `src/TutorSphere.Application/Services/ExpertGovernanceAuditService.cs` |
| `src/TutorSphere.Application/Services/ExpertModuleFeatureService.cs` *(flags)* |
| `src/TutorSphere.Web/Services/ExpertModuleFeatures.cs` *(lecture flags UI)* |
| `src/TutorSphere.Web/Components/Pages/Admin/ExpertGroupManager.razor` → `/admin/expert-groups/{GroupId}/manager` |
| `src/TutorSphere.Web/Components/Pages/GroupAdmin/Dashboard.razor` → `/group-admin/dashboard` |
| `src/TutorSphere.Web/Components/Pages/GroupAdmin/Settings.razor` → `/group-admin/settings` |
| `src/TutorSphere.Web/Components/Pages/GroupAdmin/Members.razor` → `/group-admin/members` |
| `src/TutorSphere.Web/Components/Layout/GroupAdminLayout.razor` |
| `src/TutorSphere.Web/Components/Layout/GroupAdminSidebar.razor` |
| `src/TutorSphere.Web/Components/Pages/Expert/Admissions/Index.razor` → `/expert/members/admissions` |
| `src/TutorSphere.Web/Components/Pages/Expert/Admissions/New.razor` → `/expert/members/admissions/new` |
| `src/TutorSphere.Web/Components/Pages/Expert/Admissions/Detail.razor` → `/expert/members/admissions/{Id}` |
| `src/TutorSphere.Web/Components/Pages/Expert/Admissions/Vote.razor` → `/expert/members/admissions/{Id}/vote` |
| `src/TutorSphere.Infrastructure/Migrations/{timestamp}_Lot1GroupManagerAndAudit.cs` |
| `tests/TutorSphere.UnitTests/ExpertMembershipVoteMathTests.cs` |
| `tests/TutorSphere.UnitTests/ExpertGroupManagerRulesTests.cs` |
| `tests/TutorSphere.UnitTests/TeacherRegistrationInviteGateTests.cs` |
| `tests/TutorSphere.UnitTests/ExpertCaseAssignmentAclTests.cs` |

### À modifier

| Fichier | Motif |
|---------|--------|
| `src/TutorSphere.Domain/Entities/ExpertGroup.cs` | `GroupManagerMembershipId`, `ManagerAssignedAtUtc`, `ManagerAssignedByAdminId` |
| `src/TutorSphere.Domain/Entities/ExpertGroupMember.cs` | Alignement noms si besoin (déjà MemberRole/Status/AdmissionMethod) |
| `src/TutorSphere.Domain/Entities/ExpertGroupManagerMandate.cs` | Sync avec champs groupe |
| `src/TutorSphere.Infrastructure/Persistence/ApplicationDbContext.cs` | EF config + DbSets audit/flags |
| `src/TutorSphere.Application/Common/Interfaces/IApplicationDbContext.cs` | DbSets |
| `src/TutorSphere.Application/Services/ExpertGroupManagerService.cs` | Contrat champs + historique |
| `src/TutorSphere.Application/Services/ExpertGroupService.cs` | Activation / archive / ACL |
| `src/TutorSphere.Application/Services/ExpertApprovalService.cs` | ACL assign Manager vs Expert + audit |
| `src/TutorSphere.Application/Services/ExpertMembershipGovernanceService.cs` | Audit events + routes compat |
| `src/TutorSphere.Application/DependencyInjection.cs` | DI audit + features |
| `src/TutorSphere.Api/Controllers/ExpertGroupsController.cs` | Endpoints manager page ; option SuperAdmin-only |
| `src/TutorSphere.Api/Controllers/ExpertMembershipController.cs` | Endpoints détail admission si besoin |
| `src/TutorSphere.Api/Controllers/ExpertApprovalsController.cs` | File assign filtrée + me-context |
| `src/TutorSphere.Api/Controllers/GroupGovernanceController.cs` | Ne pas étendre Lot 2 ici |
| `src/TutorSphere.Api/appsettings.json` + `appsettings.Development.json` + `appsettings.Production.json` | Section `ExpertModules` flags |
| `src/TutorSphere.Web/Components/Layout/ExpertSidebar.razor` | Masquer placeholders via flags |
| `src/TutorSphere.Web/Components/Pages/Expert/Approvals.razor` | UI affectation dossiers |
| `src/TutorSphere.Web/Components/Pages/Expert/Admissions.razor` | Rediriger ou déléguer vers nouvelles routes |
| `src/TutorSphere.Web/Components/Pages/Admin/ExpertGroups.razor` | Lien « Changer le responsable » → page manager |
| `src/TutorSphere.Web/Components/Pages/Admin/ExpertGroupMembers.razor` | Lien manager + rôles |
| `src/TutorSphere.Web/Services/AdminService.cs` | API manager |
| `src/TutorSphere.Domain/Enums/UserRole.cs` | Documenter ; **pas** ajouter Identity GroupManager sauf décision explicite (mandat = source de vérité) |

### À ne pas toucher dans le Lot 1

- Enrichissement `GroupOffer*` (versions, pays, prix) — **Lot 2**
- Attachments / SignalR chat — **Lot 3**
- Modules Qualité / Entretiens / etc. — **Lot 7+**
- Suppression de `ComingSoon.razor` tant que des flags Development en dépendent

---

## 9. Décisions produit

| # | Question | Décision | Date |
|---|----------|----------|------|
| 1 | Qui peut nommer / remplacer / suspendre le Responsable ? | **SuperAdmin seul** (pas PlatformAdmin) | 2026-08-13 |
| 2 | Rôle Identity `GroupManager` ou mandat uniquement ? | **Rôle Identity `GroupManager`** (+ mandat / membership pour historique et unicité) | 2026-08-13 |
| 3 | Production : masquer modules non prêts vs badge ? | **Ne pas masquer — les freezer** (visibles mais verrouillés) | 2026-08-13 |

### Impact décision 1

- Endpoints `POST api/admin/expert-groups/{id}/manager` et `.../manager/suspend` : restreindre à `[Authorize(Roles = UserRoles.SuperAdmin)]`.
- Création de groupe avec Responsable obligatoire : même contrainte SuperAdmin.
- `PlatformAdmin` conserve la consultation des groupes / membres / messages selon les droits existants, **sans** nomination ni transfert de mandat.
- UI Control Center : actions « Changer le responsable » / nomination visibles uniquement pour SuperAdmin.

### Impact décision 2

- Ajouter `UserRoles.GroupManager = "GroupManager"` et l’inclure dans `UserRoles.All` (seed Identity).
- À la nomination : `AddToRoleAsync(GroupManager)` **et** conserver le rôle `Expert` (le Responsable reste Expert).
- Au remplacement / fin de mandat / suspension : retirer `GroupManager` de l’ancien Responsable s’il n’a plus de mandat actif ; ne pas retirer `Expert` tant qu’il est membre actif.
- Autorisations chat Admin, catalogue publication, répartition dossiers, `/group-admin/*` : `[Authorize(Roles = UserRoles.GroupManager)]` (éventuellement combiné à la vérification d’appartenance au groupe).
- Le mandat (`ExpertGroupManagerMandate`) et `MemberRole.Manager` restent la source de vérité métier (un seul actif, historique) ; le rôle Identity est le gate d’accès API/UI.
- `api/expert/me-context` : exposer aussi `roles` / `isGroupManager` basé sur Identity + mandat actif (cohérents).
- Login Expert : un `GroupManager` se connecte via le flux Expert ; claim primaire peut rester Expert ou GroupManager selon priorité claims (à fixer : **GroupManager avant Expert** dans `BuildAuthResponse`).

### Impact décision 3 — Freezer (pas masquer)

- Les modules non prêts **restent visibles** dans le menu (Production et Development).
- État UI : lien **désactivé / non cliquable** + badge explicite **« Bientôt disponible »** (frozen).
- **Interdit** de présenter un module frozen comme disponible.
- Route `/expert/coming-soon/{module}` : soit inaccessible depuis le menu (lien frozen sans navigation), soit page frozen dédiée sans promesse de fonctionnalité — pas de faux CRUD.
- Feature flags : `Enabled` vs `Frozen` (pas `Hidden` en Production pour ces modules).
- Quand un module passe à Completed : retirer le freeze, activer le NavLink, supprimer la dépendance coming-soon.

### Décisions 1–3 : audit validé — **Phase 0 + Lot 1A démarrés (2026-08-13)**

Livré :
- Feature flags `ExpertModules` + menu freezé (badge, non cliquable)
- Rôle Identity `GroupManager` (seed via `UserRoles.All`)
- Claims : priorité GroupManager avant Expert
- Nomination / création groupe / suspend : **SuperAdmin seul**
- Champs `GroupManagerMembershipId`, `ManagerAssignedAtUtc`, `ManagerAssignedByAdminId`
- Pages `/admin/expert-groups/{id}/manager`, `/group-admin/dashboard|settings|members`
- Chat API/UI : rôle `GroupManager` (403 Expert ordinaire)
- Migration `Lot1A_GroupManagerIdentityAndFields`

Suite Lot 1 : admissions routes détaillées, affectation dossiers UI, audit events.

---

## 10. Prochaine action autorisée

Décisions produit **1–3 validées**. Prochaine étape d’implémentation :

1. **Phase 0** — feature flags `Enabled` / `Frozen` + menu Expert freezé (badge, non cliquable)
2. Puis **Phase 1A** — Responsable (`GroupManager` Identity + SuperAdmin-only + pages)

Ne pas démarrer Lot 2 / Lot 3 avant critères de fin du Lot 1.
