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
| `STRIPE__SECRETKEY` | Clé secrète Stripe |
| `STRIPE__PUBLISHABLEKEY` | Clé publique Stripe |
| `STRIPE__WEBHOOKSECRET` | Secret de webhook Stripe |
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

Dans **Settings → Secrets and variables → Actions** du dépôt, créez les secrets suivants (noms exacts).

### Application (runtime)

| Secret GitHub | Description |
|---------------|-------------|
| `CONNECTIONSTRINGS__DEFAULTCONNECTION` | Chaîne de connexion PostgreSQL de production (Npgsql) |
| `JWT__KEY` | Clé JWT de production (min. 32 caractères) |
| `JWT__ISSUER` | Émetteur JWT (ex. `TutorSphere`) |
| `JWT__AUDIENCE` | Audience JWT (ex. `TutorSphere`) |
| `STRIPE__SECRETKEY` | Clé secrète Stripe live ou test |
| `STRIPE__PUBLISHABLEKEY` | Clé publique Stripe |
| `STRIPE__WEBHOOKSECRET` | Secret du endpoint webhook Stripe |
| `APIBASEURL` | URL publique de l'API (ex. `https://api.tutorsphere.com`) |

### Déploiement SSH (serveur Linux)

| Secret GitHub | Obligatoire | Description |
|---------------|-------------|-------------|
| `SSH_HOST` | Oui | Adresse IP ou nom d'hôte du serveur |
| `SSH_USER` | Oui | Utilisateur SSH (ex. `deploy`) |
| `SSH_PRIVATE_KEY` | Oui | Clé privée SSH (contenu complet, format PEM) |
| `DEPLOY_PATH` | Oui | Répertoire de déploiement (ex. `/var/www/tutorsphere`) |
| `API_PORT` | Non | Port local de l'API (défaut : `55099`) |
| `WEB_PORT` | Non | Port local du Web (défaut : `55010`) |
| `SERVICE_USER` | Non | Utilisateur systemd (défaut : `tutorsphere`) |

Le workflow `.github/workflows/ci.yml` :

- **build-and-test** : compile et exécute les tests sur chaque push/PR vers `main` (aucun secret requis)
- **publish** (push `main`) : publie les artefacts API et Web
- **deploy** (push `main`) : synchronise les fichiers via SSH/rsync, écrit le fichier d'environnement et redémarre les services systemd

> **Important** : les secrets d'application sont injectés dans `DEPLOY_PATH/env` sur le serveur à chaque déploiement. L'application les lit au **runtime** via `EnvironmentFile` systemd.

## Déploiement sur serveur Linux

Prérequis sur le serveur :

- .NET 10 runtime (`dotnet-runtime-10.0`)
- PostgreSQL accessible (base `TutorSphere` créée, utilisateur dédié recommandé)
- nginx (reverse proxy) — voir `deploy/nginx/tutorsphere.conf.example`
- L'utilisateur `SSH_USER` doit pouvoir écrire dans `DEPLOY_PATH` et exécuter `sudo` sans mot de passe pour `deploy/deploy.sh` (installation systemd)

### Première configuration manuelle (une fois)

```bash
# Sur le serveur, en tant que root ou avec sudo
sudo mkdir -p /var/www/tutorsphere
sudo chown deploy:deploy /var/www/tutorsphere   # remplacer deploy par SSH_USER

# Autoriser le déploiement sans mot de passe (exemple pour l'utilisateur deploy)
echo 'deploy ALL=(ALL) NOPASSWD: /var/www/tutorsphere/deploy/deploy.sh' | sudo tee /etc/sudoers.d/tutorsphere-deploy

# nginx
sudo cp /var/www/tutorsphere/deploy/nginx/tutorsphere.conf.example /etc/nginx/sites-available/tutorsphere
# Éditer les domaines et ports, puis :
sudo ln -s /etc/nginx/sites-available/tutorsphere /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx

# TLS (optionnel mais recommandé)
sudo certbot --nginx -d api.tutorsphere.example.com -d app.tutorsphere.example.com
```

### Déploiement automatique

Chaque push sur `main` déclenche le pipeline CI/CD :

1. Build et tests
2. Publication `dotnet publish` (API + Web)
3. Rsync vers `DEPLOY_PATH/api`, `DEPLOY_PATH/web`, `DEPLOY_PATH/deploy`
4. Upload de `env` depuis les secrets GitHub
5. Exécution de `deploy/deploy.sh` (systemd : `tutorsphere-api`, `tutorsphere-web`)

Les migrations EF Core s'exécutent au démarrage de l'API (`Database.MigrateAsync`).

### Déploiement manuel

```bash
# Depuis une machine avec accès SSH
export DEPLOY_PATH=/var/www/tutorsphere
rsync -avz publish/api/ user@server:${DEPLOY_PATH}/api/
rsync -avz publish/web/ user@server:${DEPLOY_PATH}/web/
rsync -avz deploy/ user@server:${DEPLOY_PATH}/deploy/
scp deploy/env.example user@server:${DEPLOY_PATH}/env   # éditer les valeurs avant
ssh user@server "sudo DEPLOY_PATH=${DEPLOY_PATH} bash ${DEPLOY_PATH}/deploy/deploy.sh"
```

### Vérification

```bash
sudo systemctl status tutorsphere-api tutorsphere-web
curl -s http://127.0.0.1:55099/openapi/v1.json | head   # si OpenAPI activé en prod
curl -I http://127.0.0.1:55010
```

Fichiers utiles :

| Fichier | Rôle |
|---------|------|
| `deploy/deploy.sh` | Script serveur (permissions, systemd, redémarrage) |
| `deploy/systemd/*.service.template` | Modèles unités systemd |
| `deploy/nginx/tutorsphere.conf.example` | Exemple reverse proxy nginx |
| `deploy/env.example` | Modèle variables d'environnement |
