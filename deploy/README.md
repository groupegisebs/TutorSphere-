# Déploiement serveur Linux (Docker Compose)

Le déploiement production se fait via **GitHub Actions** (push sur `main` ou déclenchement manuel).

## GitHub Actions (production)

| Fichier | Rôle |
|---------|------|
| `.github/workflows/ci.yml` | Build, tests, validation images Docker |
| `.github/workflows/deploy-production.yml` | Déploiement Docker via SSH |
| `deploy/deploy-gha.sh` | Script bash exécuté par le workflow |
| `deploy/build-app-env.sh` | Génère `.env` depuis les secrets GitHub |
| `deploy/gha-env.sh` | Sanitisation variables CI |
| `deploy/database.defaults.sh` | Base PostgreSQL `TutorSphere` |
| `deploy/GITHUB-SECRETS.md` | Secrets à configurer dans GitHub |

### Déclencher un déploiement

1. **Automatique** : merge / push sur la branche `main`
2. **Manuel** : GitHub → **Actions** → **Deploy Production** → **Run workflow**

### Vérification

```bash
ssh ubuntu@51.79.53.197
cd /opt/apps/tutorsphere/app
docker compose -f docker-compose.yml -f docker-compose.prod.yml ps
curl -s http://127.0.0.1:55099/health
curl -s http://127.0.0.1:55010/health
```

---

## Développement local (Docker)

```bash
cp deploy/env.example .env
# Éditer .env (PostgreSQL local ou distant)
docker compose up --build
```

| Service | URL |
|---------|-----|
| API | http://localhost:5099 |
| Web | http://localhost:5010 |

PostgreSQL n'est **pas** inclus dans Compose — utilisez une instance locale ou le serveur partagé via `CONNECTIONSTRINGS__DEFAULTCONNECTION`.

---

## Déploiement manuel sur le serveur (secours)

```bash
sudo mkdir -p /opt/apps/tutorsphere/app
sudo cp deploy/env.example /opt/apps/tutorsphere/app/.env
# Éditer .env avec les secrets de production
sudo chown ubuntu:ubuntu /opt/apps/tutorsphere/app/.env
chmod 600 /opt/apps/tutorsphere/app/.env

git pull
./deploy/deploy.sh
```

---

## Structure sur le serveur

```
/opt/apps/tutorsphere/
├── app/
│   ├── .env                  # Secrets injectés par GitHub Actions
│   ├── docker-compose.yml
│   ├── docker-compose.prod.yml
│   ├── src/                  # Contexte de build Docker
│   └── .dockerignore
└── backups/                  # Sauvegardes .env (deploy-gha.sh)
```

---

## Première installation serveur

```bash
sudo mkdir -p /opt/apps/tutorsphere/app
sudo chown -R ubuntu:ubuntu /opt/apps/tutorsphere

# Docker Engine + Compose plugin requis
docker --version
docker compose version
```

L'utilisateur `ubuntu` doit pouvoir exécuter `docker` (groupe `docker`).

Reverse proxy (Nginx Proxy Manager ou nginx) — voir section ci-dessous et `deploy/nginx/NPM.md`.

---

## Configuration Nginx Proxy Manager

TutorSphere production écoute sur l’hôte (`network_mode: host`), **pas** sur les ports dev `5010` / `5099`.

| Domaine | Forward Host | Forward Port | Notes |
|---------|--------------|--------------|-------|
| `tutorsphere.gisebs.com` | `172.17.0.1` | **55010** | Web Blazor — Websockets ✓ |
| `api.tutorsphere.gisebs.com` | `172.17.0.1` | **55099** | API REST + SignalR — Websockets ✓ |

`172.17.0.1` est la passerelle Docker vers l’hôte (même modèle que giseboutique `:5001`, comptadoc `:5050`). Alternative : `127.0.0.1` si NPM n’est pas conteneurisé.

**Secret GitHub** (optionnel, défaut déjà correct) :

```
TUTORSPHERE_API_BASE_URL=https://api.tutorsphere.gisebs.com
```

Guide pas à pas : [`deploy/nginx/NPM.md`](nginx/NPM.md). Exemple nginx natif : [`deploy/nginx/tutorsphere.conf.example`](nginx/tutorsphere.conf.example).

### Dépannage 502

1. Vérifier que NPM n’utilise **pas** `:5010` (port dev).
2. Sur le serveur : `curl http://127.0.0.1:55010/health` et `:55099/health` → HTTP 200.
3. Si les healthchecks échouent : relancer **Deploy Production** (GitHub Actions).

---

## Base de données

Base PostgreSQL dédiée : **`TutorSphere`** sur `51.79.53.197:5432` (serveur partagé GISEBS).

Les migrations EF s'exécutent au démarrage de l'API (`Database.MigrateAsync`).

---

## Fichiers du dossier `deploy/`

| Fichier | Rôle |
|---------|------|
| `deploy-gha.sh` / `gha-env.sh` | Déploiement GitHub Actions |
| `deploy.sh` | Déploiement manuel sur le serveur |
| `build-app-env.sh` | Génération `.env` depuis secrets CI |
| `database.defaults.sh` | Constantes PostgreSQL |
| `GITHUB-SECRETS.md` | Secrets GitHub |
| `env.example` | Modèle `.env` (sans secrets) |
| `nginx/NPM.md` | Configuration Nginx Proxy Manager (GISEBS) |
| `nginx/tutorsphere.conf.example` | Exemple reverse proxy nginx natif |

---

## Dépannage

```bash
cd /opt/apps/tutorsphere/app
docker compose -f docker-compose.yml -f docker-compose.prod.yml logs -f api
docker compose -f docker-compose.yml -f docker-compose.prod.yml logs -f web
curl -v http://127.0.0.1:55099/health
```

Restaurer un `.env` précédent :

```bash
cp /opt/apps/tutorsphere/backups/env.<TIMESTAMP> /opt/apps/tutorsphere/app/.env
./deploy/deploy.sh
```
