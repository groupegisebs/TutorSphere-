# Secrets GitHub — TutorSphere

Le workflow **Deploy Production** échoue tant que les secrets ci-dessous ne sont pas configurés.

**Lien direct** : `https://github.com/<org>/<repo>/settings/secrets/actions`

*(Même modèle que Boutique GISEBS — voir `BoutiqueGisie/deploy/GITHUB-SECRETS.md`.)*

---

## Créer la base PostgreSQL (SSH, une fois)

```bash
ssh ubuntu@51.79.53.197
sudo -u postgres psql -v ON_ERROR_STOP=1 -c 'CREATE DATABASE "TutorSphere" OWNER gisedocuser;'
sudo -u postgres psql -d TutorSphere -c 'GRANT ALL ON SCHEMA public TO gisedocuser;'
```

---

## Secrets obligatoires

| Secret | Description | Exemple |
|--------|-------------|---------|
| `TUTORSPHERE_CONNECTION_STRING` | PostgreSQL — base **`TutorSphere`** | `Host=51.79.53.197;Port=5432;Database=TutorSphere;Username=gisedocuser;Password=...` |
| `TUTORSPHERE_JWT_KEY` | Clé JWT (min. 32 caractères) | chaîne aléatoire longue |
| `TUTORSPHERE_PAYGATEWAY_BASE_URL` | URL publique du Pay Gateway (nginx HTTPS) | `https://gisebsapipaygateway.gisebs.com` |
| `TUTORSPHERE_PAYGATEWAY_API_KEY` | Clé API app `TUTORSPHERE` | `gbsk_...` |

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
| `TUTORSPHERE_API_BASE_URL` | `http://127.0.0.1:55099` (URL publique API pour Blazor) |

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

```bash
ssh ubuntu@51.79.53.197
sudo mkdir -p /opt/apps/tutorsphere
sudo chown ubuntu:ubuntu /opt/apps/tutorsphere

# Docker Engine + Compose plugin requis
docker --version
docker compose version
```

Le workflow écrit `/opt/apps/tutorsphere/app/.env` (chmod 600) avec la connection string, JWT et PayGateway — **rien de sensible dans le dépôt**.

Reverse proxy (nginx / Nginx Proxy Manager) : `http://127.0.0.1:55099` (API) et `http://127.0.0.1:55010` (Web) — voir `deploy/nginx/tutorsphere.conf.example`.

---

## Vérification

1. **Actions** → **Deploy Production** → **Run workflow**
2. Étape **Diagnose secrets** : tous les `OK`
3. Healthcheck sur le serveur :

```bash
curl -s http://127.0.0.1:55099/health
curl -s http://127.0.0.1:55010/health
```

4. Healthcheck Pay Gateway : `https://gisebsapipaygateway.gisebs.com/health` → `Healthy`

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

## Checklist

- [ ] `SSH_PRIVATE_KEY_UBUNTU1` (org) ou `TUTORSPHERE_SSH_PRIVATE_KEY`
- [ ] `TUTORSPHERE_CONNECTION_STRING` avec `Database=TutorSphere`
- [ ] `TUTORSPHERE_JWT_KEY` (min. 32 caractères)
- [ ] `TUTORSPHERE_PAYGATEWAY_BASE_URL` = `https://gisebsapipaygateway.gisebs.com`
- [ ] `TUTORSPHERE_PAYGATEWAY_API_KEY`
- [ ] App `TUTORSPHERE` dans PayGateway
- [ ] Répertoire `/opt/apps/tutorsphere` sur le serveur
- [ ] Base PostgreSQL `TutorSphere` créée
- [ ] Docker installé, utilisateur `ubuntu` dans le groupe `docker`
- [ ] Re-run du workflow Deploy Production
