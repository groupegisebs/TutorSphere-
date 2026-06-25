# TutorSphere

Plateforme SaaS multi-tenant permettant aux répétiteurs indépendants de créer leur école numérique, de gérer leurs élèves et abonnements, tout en offrant aux parents et élèves un environnement simple pour l'inscription et le suivi pédagogique.

## Vision

TutorSphere n'est pas une simple application de réservation — c'est un **ERP spécialisé pour le soutien scolaire**, conçu pour gérer l'ensemble de l'activité d'un répétiteur comme une véritable entreprise.

## Architecture

```
TutorSphere/
├── src/
│   ├── TutorSphere.Domain/          # Entités, enums, interfaces métier
│   ├── TutorSphere.Application/     # Services, DTOs, cas d'usage
│   ├── TutorSphere.Infrastructure/  # EF Core, Identity, multi-tenancy
│   ├── TutorSphere.Api/             # API REST (JWT, OpenAPI)
│   └── TutorSphere.Web/             # Interface Blazor
├── tests/
│   └── TutorSphere.UnitTests/
└── docs/
```

### Multi-tenancy

Chaque répétiteur dispose de son propre espace isolé (`TenantId`). La résolution du tenant s'effectue via :

- En-tête HTTP `X-Tenant-Slug`
- Paramètre de requête `?tenant=slug`
- Sous-domaine `jean.tutorsphere.com`

### Rôles

| Rôle | Description |
|------|-------------|
| SuperAdmin | Gestion complète de la plateforme |
| PlatformAdmin | Support, validation, paiements |
| Tutor | Propriétaire de l'espace répétiteur |
| TeachingAssistant | Assistant pédagogique |
| Parent | Gestion familiale |
| Student | Accès pédagogique |

## Démarrage rapide

### Prérequis

- .NET 10 SDK
- SQL Server LocalDB (inclus avec Visual Studio) ou SQL Server

### Lancer l'API

```bash
dotnet run --project src/TutorSphere.Api
```

L'API démarre sur `https://localhost:7xxx` avec OpenAPI en développement.

**Compte super-admin par défaut :** `admin@tutorsphere.com` / `Admin123!`

### Lancer l'interface web

```bash
dotnet run --project src/TutorSphere.Web
```

### Configuration

Les secrets (base de données, JWT, Stripe) ne sont pas dans le dépôt. Voir [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) pour la configuration locale et les [secrets GitHub Actions](docs/DEVELOPMENT.md#secrets-github-actions).

### Endpoints principaux

| Méthode | Route | Description |
|---------|-------|-------------|
| POST | `/api/auth/register` | Inscription utilisateur |
| POST | `/api/auth/login` | Connexion JWT |
| POST | `/api/tenants` | Créer une école (tenant) |
| GET | `/api/tenants/{slug}` | Consulter un tenant |
| GET | `/api/tenants/{id}/dashboard` | Tableau de bord répétiteur |

## Modules planifiés

- [x] Fondation multi-tenant et authentification JWT
- [x] Modèle de domaine (élèves, abonnements, cours, paiements)
- [x] Interface Blazor (accueil, dashboards)
- [ ] Intégration Stripe Connect
- [ ] Calendrier et gestion des cours
- [ ] Devoirs et rapports pédagogiques
- [ ] Messagerie et notifications temps réel
- [ ] Site personnalisé par répétiteur
- [ ] Recherche publique de répétiteurs
- [ ] Applications mobiles (Android / iOS)
- [ ] Internationalisation (FR, EN, ES, PT, AR)

## Modèle économique

**Répétiteurs :** Starter (14,99 $), Professional (29,99 $), Business (59,99 $), Enterprise (sur devis)

**Parents :** Paient uniquement les abonnements choisis

**Commission plateforme :** 5 % à 15 % (paramétrable par tenant)

## Licence

Propriétaire — Groupe Gisebs
