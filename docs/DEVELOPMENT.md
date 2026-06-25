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

1. **Développement local** — `appsettings.Development.json` (JWT et PostgreSQL local par défaut) + optionnellement :
   - `appsettings.Development.local.json` (copier depuis `appsettings.Development.local.json.example`, fichier ignoré par Git)
   - [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) pour l'API et le Web

2. **Production / CI** — variables d'environnement (convention ASP.NET Core `Section__Key`) :

| Variable d'environnement | Configuration |
|--------------------------|---------------|
| `CONNECTIONSTRINGS__DEFAULTCONNECTION` | Chaîne de connexion PostgreSQL (Npgsql) |
| `JWT__KEY` | Clé de signature JWT (min. 32 caractères) |
| `JWT__ISSUER` | Émetteur JWT |
| `JWT__AUDIENCE` | Audience JWT |
| `PAYGATEWAY__BASEURL` | URL de base de la passerelle GiseBs Pay Gateway |
| `PAYGATEWAY__APPCODE` | Code application enregistré dans la passerelle |
| `PAYGATEWAY__APIKEY` | Clé API (`gbsk_...`) de la passerelle |
| `APIBASEURL` | URL de base de l'API (projet Web) |

### Chaîne de connexion PostgreSQL

Format Npgsql (même serveur que les autres applications) :

```
Host=<hostname>;Port=5432;Database=TutorSphere;Username=<user>;Password=<password>
```

**Développement local** — valeur par défaut dans `appsettings.Development.json` :

```
Host=localhost;Port=5432;Database=TutorSphere;Username=postgres;Password=postgres
```

Pour un serveur partagé ou des identifiants différents, définissez la variable d'environnement ou un secret local :

```bash
# PowerShell
$env:CONNECTIONSTRINGS__DEFAULTCONNECTION = "Host=db.example.com;Port=5432;Database=TutorSphere;Username=tutorsphere;Password=***"

# User Secrets (API)
cd src/TutorSphere.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=db.example.com;Port=5432;Database=TutorSphere;Username=tutorsphere;Password=***"
```

**GitHub Actions / production** — secret `CONNECTIONSTRINGS__DEFAULTCONNECTION` avec le même format Npgsql.

### Passerelle de paiement (GiseBs Pay Gateway)

TutorSphere ne communique plus directement avec Stripe. Les paiements passent par [GiseBs Pay Gateway](https://gisebsapipaygateway.gisebs.com) via l'API HTTP authentifiée (`X-App-Code`, `X-Api-Key`).

**Flux :**

1. `POST /api/payments/subscriptions/{id}/checkout` — crée un paiement local et une session Checkout Stripe via la passerelle
2. Redirection du parent vers `checkoutUrl` retourné
3. `GET /api/payments/{paymentId}/status` — synchronise l'état local en interrogeant `GET /api/payments/{paymentCode}` sur la passerelle

Les webhooks Stripe sont reçus par la passerelle (`POST /api/webhooks/stripe`), pas par TutorSphere. Après le paiement, le client (ou le front) doit appeler l'endpoint de synchronisation.

**Enregistrement dans la passerelle :** créer une application `TUTORSPHERE` dans l'admin de la passerelle et récupérer la clé `gbsk_...`.


**API** — passerelle de paiement et autres overrides :

```bash
cd src/TutorSphere.Api
dotnet user-secrets set "PayGateway:BaseUrl" "https://gisebsapipaygateway.gisebs.com"
dotnet user-secrets set "PayGateway:AppCode" "TUTORSPHERE"
dotnet user-secrets set "PayGateway:ApiKey" "gbsk_..."
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

## Docker

### Développement local

```bash
cp deploy/env.example .env
# Éditer CONNECTIONSTRINGS__DEFAULTCONNECTION, JWT__KEY, PayGateway, etc.
docker compose up --build
```

PostgreSQL n'est **pas** conteneurisé — pointez `CONNECTIONSTRINGS__DEFAULTCONNECTION` vers votre instance locale ou le serveur partagé.

Fichiers :

| Fichier | Rôle |
|---------|------|
| `docker-compose.yml` | Dev local (ports 5099 / 5010) |
| `docker-compose.prod.yml` | Surcharge production (réseau host, ports 55099 / 55010) |
| `src/TutorSphere.Api/Dockerfile` | Image API |
| `src/TutorSphere.Web/Dockerfile` | Image Blazor Web |
| `.dockerignore` | Contexte de build |

### Déploiement Docker (production)

Le workflow **Deploy Production** (`.github/workflows/deploy-production.yml`) :

1. Build Release .NET
2. Génère `/tmp/tutorsphere.app.env` via `deploy/build-app-env.sh`
3. Exécute `deploy/deploy-gha.sh` : rsync sources + compose, build et `up -d` sur le serveur

Le workflow **CI** (`.github/workflows/ci.yml`) valide build, tests et images Docker sur chaque push/PR.

Structure serveur : `/opt/apps/tutorsphere/app/` — voir [deploy/README.md](../deploy/README.md) et [deploy/GITHUB-SECRETS.md](../deploy/GITHUB-SECRETS.md).

## Secrets et variables GitHub Actions

Même modèle que **Boutique GISEBS** — détail complet dans [deploy/GITHUB-SECRETS.md](../deploy/GITHUB-SECRETS.md).

### Secrets obligatoires (dépôt)

| Secret | Description |
|--------|-------------|
| `TUTORSPHERE_CONNECTION_STRING` | PostgreSQL (`Database=TutorSphere`) |
| `TUTORSPHERE_JWT_KEY` | Clé JWT (min. 32 caractères) |
| `TUTORSPHERE_PAYGATEWAY_BASE_URL` | ex. `https://gisebsapipaygateway.gisebs.com` |
| `TUTORSPHERE_PAYGATEWAY_API_KEY` | Clé `gbsk_...` app TUTORSPHERE |

### Secret SSH (un seul suffit)

| Secret | Source |
|--------|--------|
| `SSH_PRIVATE_KEY_UBUNTU1` | Organisation GISEBS |
| `TUTORSPHERE_SSH_PRIVATE_KEY` | Secret propre au dépôt |

### Variables optionnelles (org ou dépôt)

| Variable | Défaut |
|----------|--------|
| `SSH_HOST_UBUNTU1` | `51.79.53.197` |
| `SSH_USER_UBUNTU1` | `ubuntu` |
| `SSH_PORT_UBUNTU1` | `22` |
| `TUTORSPHERE_APP_ROOT` | `/opt/apps/tutorsphere` |
| `TUTORSPHERE_API_PORT` | `55099` |
| `TUTORSPHERE_WEB_PORT` | `55010` |

> **Important** : les secrets d'application sont injectés dans `/opt/apps/tutorsphere/app/.env` sur le serveur à chaque déploiement.

## Déploiement sur serveur Linux (Docker)

Prérequis sur le serveur :

- **Docker Engine** + plugin **Compose** (`docker compose version`)
- PostgreSQL accessible (base `TutorSphere` sur `51.79.53.197:5432`)
- Utilisateur SSH membre du groupe `docker`
- nginx ou Nginx Proxy Manager — voir `deploy/nginx/tutorsphere.conf.example`

### Première configuration manuelle (une fois)

```bash
sudo mkdir -p /opt/apps/tutorsphere/app
sudo chown -R ubuntu:ubuntu /opt/apps/tutorsphere
sudo cp deploy/env.example /opt/apps/tutorsphere/app/.env
# Éditer .env avec les secrets de production
chmod 600 /opt/apps/tutorsphere/app/.env
```

### Déploiement automatique

Chaque push sur `main` déclenche le workflow **Deploy Production** (`.github/workflows/deploy-production.yml`).

Le workflow **CI** (`.github/workflows/ci.yml`) exécute build, tests et validation Docker sur chaque push/PR.

Les migrations EF Core s'exécutent au démarrage de l'API (`Database.MigrateAsync`).

### Déploiement manuel

```bash
git pull
./deploy/deploy.sh
```

### Vérification

```bash
cd /opt/apps/tutorsphere/app
docker compose -f docker-compose.yml -f docker-compose.prod.yml ps
curl -s http://127.0.0.1:55099/health
curl -s http://127.0.0.1:55010/health
```

Fichiers utiles :

| Fichier | Rôle |
|---------|------|
| `deploy/deploy-gha.sh` | Script CI (rsync + docker compose) |
| `deploy/deploy.sh` | Script serveur manuel |
| `deploy/build-app-env.sh` | Génération `.env` depuis secrets CI |
| `deploy/nginx/tutorsphere.conf.example` | Exemple reverse proxy nginx |
| `deploy/env.example` | Modèle variables d'environnement |
| `deploy/GITHUB-SECRETS.md` | Liste des secrets GitHub |
