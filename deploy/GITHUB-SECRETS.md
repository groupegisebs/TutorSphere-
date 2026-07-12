# Secrets GitHub — TutorSphere

Le workflow **Deploy Production** échoue tant que les secrets ci-dessous ne sont pas configurés.

**Lien direct** : `https://github.com/<org>/<repo>/settings/secrets/actions`

*(Même modèle que Boutique GISEBS — voir `BoutiqueGisie/deploy/GITHUB-SECRETS.md`.)*

---

## Secrets obligatoires

| Secret | Description | Exemple |
|--------|-------------|---------|
| `TUTORSPHERE_CONNECTION_STRING` | PostgreSQL — base **`TutorSphere`** | `Host=51.79.53.197;Port=5432;Database=TutorSphere;Username=gisedocuser;Password=...` |
| `TUTORSPHERE_JWT_KEY` | Clé JWT (min. 32 caractères) | chaîne aléatoire longue |
| `TUTORSPHERE_PAYGATEWAY_BASE_URL` | URL publique du Pay Gateway (nginx HTTPS) | `https://gisebsapipaygateway.gisebs.com` |
| `TUTORSPHERE_PAYGATEWAY_API_KEY` | Clé API app `TUTORSPHERE` | `gbsk_...` |

> **Stripe :** en production (`ASPNETCORE_ENVIRONMENT=Production`), TutorSphere **n'envoie pas** `X-Stripe-Env: DEV` → Stripe Live. Le bac à sable est réservé à Development / Staging (ou override `PAYGATEWAY__USESANDBOX` en QA uniquement).

> **Ne pas utiliser** `giseboutique.gisebs.com` (boutique) ni le port interne `http://51.79.53.197:7843` — l'API est exposée via **[GISEBS Pay Gateway](https://gisebsapipaygateway.gisebs.com/)**.

## Secret SSH (un seul suffit)

| Secret | Source |
|--------|--------|
| `SSH_PRIVATE_KEY_UBUNTU1` | Organisation GISEBS (clé partagée `cognidoc_deploy`) |
| `TUTORSPHERE_SSH_PRIVATE_KEY` | Secret propre au dépôt |

---

## Optionnels (défauts dans le workflow)

| Variable / secret | Défaut |
|-------------------|--------|
| Serveur (`SSH_HOST_UBUNTU1`) | `51.79.53.197` |
| User SSH (`SSH_USER_UBUNTU1`) | `ubuntu` |
| Port SSH (`SSH_PORT_UBUNTU1`) | `22` |
| App root (`TUTORSPHERE_APP_ROOT`) | `/opt/apps/tutorsphere` |
| Port API (`TUTORSPHERE_API_PORT`) | `55099` |
| Port Web (`TUTORSPHERE_WEB_PORT`) | `55010` |
| `TUTORSPHERE_PAYGATEWAY_APP_CODE` | `TUTORSPHERE` |
| `TUTORSPHERE_API_BASE_URL` | `https://api.tutorsphere.gisebs.com` (URL publique API pour le navigateur Blazor) |

Host/User/Port SSH : secret org, variable org, ou défaut workflow (`51.79.53.197` / `ubuntu` / `22`).

---

## Création des secrets (CLI)

```bash
gh secret set TUTORSPHERE_CONNECTION_STRING --body "Host=51.79.53.197;Port=5432;Database=TutorSphere;Username=gisedocuser;Password=VOTRE_MDP"
gh secret set TUTORSPHERE_JWT_KEY --body "VOTRE_CLE_JWT_MIN_32_CARACTERES"
gh secret set TUTORSPHERE_PAYGATEWAY_BASE_URL --body "https://gisebsapipaygateway.gisebs.com"
gh secret set TUTORSPHERE_PAYGATEWAY_API_KEY --body "gbsk_votre_cle"
```

La clé PayGateway se génère dans l'admin **[GISEBS Pay Gateway](https://gisebsapipaygateway.gisebs.com/)** → Applications → **TUTORSPHERE**.

---

## Sur le serveur (une fois)

Le workflow crée automatiquement `/opt/apps/tutorsphere/app` et la base PostgreSQL `TutorSphere` si nécessaire.

Prérequis serveur : **Docker Engine** + plugin Compose (`docker compose`) **ou** binaire `docker-compose`, utilisateur `ubuntu` dans le groupe `docker`.

Le workflow écrit `/opt/apps/tutorsphere/app/.env` (chmod 600) avec la connection string, JWT et PayGateway — **rien de sensible dans le dépôt**.

---

## Nginx Proxy Manager (production GISEBS)

Sur le serveur partagé `51.79.53.197`, NPM route le trafic HTTPS vers les ports **production** Docker (réseau host) :

| Domaine NPM | Forward | Port |
|-------------|---------|------|
| `tutorsphere.gisebs.com` | `172.17.0.1` | **55010** (Web) |
| `api.tutorsphere.gisebs.com` | `172.17.0.1` | **55099** (API) |

Activer **Websockets Support** sur les deux hosts (Blazor `/_blazor`, SignalR `/hubs/`).

> **502 Bad Gateway** si NPM pointe vers `:5010` — c’est le port **dev local**, pas la production.

Guide détaillé : [`deploy/nginx/NPM.md`](nginx/NPM.md).

Reverse proxy nginx natif (alternative) : `deploy/nginx/tutorsphere.conf.example`.

---

## Vérification

1. Configurer **tous** les secrets obligatoires (voir checklist ci-dessous)
2. **Push sur `main`** — le workflow **Deploy Production** se lance automatiquement
3. Étape **Diagnose secrets** : tous les `OK`
4. Étape **Deploy** : messages `Staging OK` et `App OK`
5. Healthcheck sur le serveur (optionnel) :

```bash
curl -s http://127.0.0.1:55099/health
curl -s http://127.0.0.1:55010/health
```

6. Healthcheck Pay Gateway : `https://gisebsapipaygateway.gisebs.com/health` → `Healthy`

### Dépannage paiement — erreur « PayGateway (404) »

| Symptôme | Cause | Correction |
|----------|--------|------------|
| **404** au checkout | `TUTORSPHERE_PAYGATEWAY_BASE_URL` = URL de la boutique | Mettre `https://gisebsapipaygateway.gisebs.com` |
| **401** | Clé API invalide ou révoquée | Régénérer la clé `gbsk_…` dans Pay Gateway → **TUTORSPHERE** |
| Connexion refusée | Port interne `:7843` utilisé depuis l'extérieur | Utiliser l'URL HTTPS publique (nginx) |

Test rapide :

```bash
curl -s "https://gisebsapipaygateway.gisebs.com/health"
curl -s -o /dev/null -w "%{http_code}" -X POST "https://gisebsapipaygateway.gisebs.com/api/auth/token" \
  -H "Content-Type: application/json" \
  -d '{"appCode":"TUTORSPHERE","apiKey":"VOTRE_CLE"}'
```

Réponse attendue sur `/api/auth/token` : **200** (token) ou **401** (mauvaise clé) — **pas 404**.

---

## Dépannage déploiement

| Symptôme | Cause probable | Correction |
|----------|----------------|------------|
| `/opt/apps/tutorsphere/app` **vide** | Transfert scp sans clé SSH ou workflow arrêté avant Deploy | Vérifier `SSH_PRIVATE_KEY_UBUNTU1` ; relancer via push sur `main` |
| `unknown shorthand flag: 'f'` | Plugin `docker compose` absent — Docker interprète `-f` comme option de `docker` | Le script détecte automatiquement `docker-compose` (tiret) ; installer l’un des deux sur le serveur |
| **502** sur `tutorsphere.gisebs.com` | NPM pointe vers `:5010` (dev) au lieu de **`:55010`** | NPM → Forward `172.17.0.1:55010` (Web) et `:55099` (API) |
| Healthcheck 127.0.0.1 OK, 172.17.0.1 KO | Apps écoutent sur `127.0.0.1` seulement | `docker-compose.prod.yml` doit avoir `ASPNETCORE_URLS=http://0.0.0.0:55010` (déjà le cas) |

---

## Checklist

- [ ] `SSH_PRIVATE_KEY_UBUNTU1` (org) ou `TUTORSPHERE_SSH_PRIVATE_KEY`
- [ ] `TUTORSPHERE_CONNECTION_STRING` avec `Database=TutorSphere`
- [ ] `TUTORSPHERE_JWT_KEY` (min. 32 caractères)
- [ ] `TUTORSPHERE_PAYGATEWAY_BASE_URL` = `https://gisebsapipaygateway.gisebs.com`
- [ ] `TUTORSPHERE_PAYGATEWAY_API_KEY`
- [ ] App `TUTORSPHERE` dans PayGateway
- [ ] Répertoire `/opt/apps/tutorsphere` (créé automatiquement par le workflow si absent)
- [ ] Base PostgreSQL `TutorSphere` (créée automatiquement au premier déploiement)
- [ ] Docker installé, utilisateur `ubuntu` dans le groupe `docker`
- [ ] Push sur `main` déclenche **Deploy Production**
- [ ] NPM : `tutorsphere.gisebs.com` → `172.17.0.1:55010` (Websockets ✓)
- [ ] NPM : `api.tutorsphere.gisebs.com` → `172.17.0.1:55099` (Websockets ✓)
- [ ] DNS A records vers `51.79.53.197`
