# Audit — Centre de gouvernance Expert / Groupes d’experts (TutorSphere)

**Date :** 2026-08-14 (rafraîchi après remédiation P0→P2)  
**Périmètre :** `TutorSphere-`  
**Statut :** remédiation de cohérence **implémentée** (ACL, lifecycle, act-as, nav, durcissement).

---

## 1. Verdict

Le parcours principal fonctionne : création groupe + Responsable → file enseignants → offres → messagerie.

Les écarts majeurs d’audit (Identity orphelin, act-as incomplet, ACL API &lt; UI, soft-deactivate incohérent, nav dupliquée) sont **corrigés** dans le code.

---

## 2. Machine d’états groupe

```text
Draft → Active ↔ Suspended → Archived
```

| Action | Mandat | Identity `GroupManager` | `LifecycleStatus` / `IsActive` |
|--------|--------|-------------------------|--------------------------------|
| Soft Désactiver | Terminé | Retiré | Suspended / false |
| Suspendre mandat | Suspended | Retiré | **inchangé** (groupe peut rester Active) |
| Archiver | Terminé | Retiré | Archived / false |
| Appoint / Transfer | Nouveau Active | Assuré | Ne réactive **pas** un groupe Suspended/Archived |

Source de vérité manager UI : **mandat Active** (ou act-as valide), plus un rôle Identity orphelin.

---

## 3. ACL & act-as (P0)

| Zone | Règle |
|------|--------|
| Admin-chat List/Post | Manager du groupe de la conversation **ou** PlatformAdmin |
| `GET teachers/{id}` | Membre du groupe reviewer (ou act-as / platform) |
| `AssignReview` | Self-claim Expert ; attribution à autrui = Responsable / act-as |
| Approve / Reject / RequestChanges / StartReview | Act-as via `X-Act-As-Expert-Group-Id` |
| Membership invites / members | Act-as supporté |
| `me-context.isGroupManager` | Mandat Active **ou** act-as valide |

---

## 4. Offres & navigation (P1)

- Create / update / publish / delete offres : **manager-only** (lecture Expert OK).
- Sidebar Responsable : hub `group-admin/*` + offres / admissions / chat / messagerie ; doublons retirés.
- Modules non prêts : flags `ExpertModules.Frozen` + `FrozenNavItem` (non cliquables).

---

## 5. Durcissement (P2)

- Index unique filtré : un mandat `Active` (Status = 1) par groupe (`IX_ExpertGroupManagerMandates_OneActivePerGroup`).
- Historique mandats : `GET api/admin/expert-groups/{id}/manager/history` + UI admin « Historique ».
- Events gouvernance étendus : invite, appoint/suspend/end, offre create/publish, chat open/message.
- Tests unitaires : `ExpertGroupGovernanceAclTests` (lifecycle, assign ACL, me-context, chat ACL).

---

## 6. Routes réelles (extrait)

| Route | État |
|-------|------|
| `/expert/dashboard`, `/approvals`, `/teachers`, `/disciplines`, `/offers`, `/members`, `/admissions`, `/admin-chat`, `/messages` | Réel |
| `/group-admin/dashboard`, `/tasks`, `/members`, `/settings` | Réel |
| `/expert/coming-soon/*` | Placeholder — **frozen** au menu |

---

## 7. Fichiers pivots

- Lifecycle / mandat : `ExpertGroupService.cs`, `ExpertGroupManagerService.cs`, `ExpertGroupsController.cs`
- Act-as : `GroupAdminAccessService.cs`, `ExpertMembershipController.cs`, `ExpertApprovalsController.cs`
- Chat ACL : `GroupAdminChatService.cs`
- Offres : `GroupOfferService.cs`
- Nav : `ExpertSidebar.razor`, `appsettings.json` `ExpertModules`

---

## 8. Déploiement

Appliquer la migration `UniqueActiveManagerMandatePerGroup` (API + Web). Vérifier qu’il n’existe pas déjà plusieurs mandats Active par groupe avant migrate.
