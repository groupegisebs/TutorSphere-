# TutorSphere — Guide de développement

## Build

```bash
dotnet build TutorSphere.slnx
```

## Run API

```bash
dotnet run --project src/TutorSphere.Api
```

## Run Web (Blazor)

```bash
dotnet run --project src/TutorSphere.Web
```

## Migrations

```bash
dotnet ef migrations add <Name> --project src/TutorSphere.Infrastructure --startup-project src/TutorSphere.Api
dotnet ef database update --project src/TutorSphere.Infrastructure --startup-project src/TutorSphere.Api
```

## Tests

```bash
dotnet test TutorSphere.slnx
```

## Configuration et secrets

Les fichiers `appsettings.json` versionnés ne contiennent **aucun secret**. Les valeurs sensibles sont fournies par :

1. **Développement local** — `appsettings.Development.json` (JWT et LocalDB uniquement) + optionnellement :
   - `appsettings.Development.local.json` (copier depuis `appsettings.Development.local.json.example`, fichier ignoré par Git)
   - [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) pour l'API et le Web

2. **Production / CI** — variables d'environnement (convention ASP.NET Core `Section__Key`) :

| Variable d'environnement | Configuration |
|--------------------------|---------------|
| `CONNECTIONSTRINGS__DEFAULTCONNECTION` | Chaîne de connexion SQL Server |
| `JWT__KEY` | Clé de signature JWT (min. 32 caractères) |
| `JWT__ISSUER` | Émetteur JWT |
| `JWT__AUDIENCE` | Audience JWT |
| `STRIPE__SECRETKEY` | Clé secrète Stripe |
| `STRIPE__PUBLISHABLEKEY` | Clé publique Stripe |
| `STRIPE__WEBHOOKSECRET` | Secret de webhook Stripe |
| `APIBASEURL` | URL de base de l'API (projet Web) |

### User Secrets (développement local)

**API** — clés Stripe et autres overrides :

```bash
cd src/TutorSphere.Api
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
```

**Web** — URL de l'API si différente du défaut :

```bash
cd src/TutorSphere.Web
dotnet user-secrets set "ApiBaseUrl" "https://localhost:7250"
```

### Fichier local (alternative)

```bash
cp src/TutorSphere.Api/appsettings.Development.local.json.example src/TutorSphere.Api/appsettings.Development.local.json
# Éditer le fichier avec vos valeurs — il est ignoré par Git
```

## Secrets GitHub Actions

Dans **Settings → Secrets and variables → Actions** du dépôt, créez les secrets suivants (noms exacts) :

| Secret GitHub | Description |
|---------------|-------------|
| `CONNECTIONSTRINGS__DEFAULTCONNECTION` | Chaîne de connexion SQL Server de production |
| `JWT__KEY` | Clé JWT de production (min. 32 caractères) |
| `JWT__ISSUER` | Émetteur JWT (ex. `TutorSphere`) |
| `JWT__AUDIENCE` | Audience JWT (ex. `TutorSphere`) |
| `STRIPE__SECRETKEY` | Clé secrète Stripe live ou test |
| `STRIPE__PUBLISHABLEKEY` | Clé publique Stripe |
| `STRIPE__WEBHOOKSECRET` | Secret du endpoint webhook Stripe |
| `APIBASEURL` | URL publique de l'API (ex. `https://api.tutorsphere.com`) |

Le workflow `.github/workflows/ci.yml` :

- **build-and-test** : compile et exécute les tests (aucun secret requis)
- **publish** (branche `main`) : publie les artefacts et expose les secrets en variables d'environnement pour les étapes de déploiement

> **Important** : configurez les mêmes variables d'environnement sur votre plateforme d'hébergement (Azure App Service, conteneur Docker, etc.) pour que l'application les lise au **runtime**.
