# Dépannage — 502 Bad Gateway (tutorsphere.gisebs.com)

Erreur typique quand **Nginx Proxy Manager (NPM)** ne peut pas joindre TutorSphere sur le serveur `51.79.53.197`.

> **Accès SSH** : si vous ne pouvez pas vous connecter en SSH (`Permission denied`, clé refusée, port fermé), vous devez quand même corriger **NPM** et lancer le **workflow GitHub** — les vérifications serveur ci-dessous sont à faire par quelqu’un ayant accès SSH ou via les logs du workflow **Deploy Production**.

---

## Checklist rapide (dans l’ordre)

### 1. Corriger NPM — Web (cause la plus fréquente)

**Nginx Proxy Manager → Hosts → Proxy Hosts → `tutorsphere.gisebs.com` → Edit**

| Champ | Valeur correcte | Erreur fréquente |
|-------|-----------------|------------------|
| **Scheme** | `http` | — |
| **Forward Hostname / IP** | `172.17.0.1` | `localhost` ou IP publique |
| **Forward Port** | **`55010`** | **`5010`** (port dev local) |
| **Websockets Support** | ✓ activé | désactivé → Blazor cassé |
| **Block Common Exploits** | ✓ | — |

**SSL** : certificat Let's Encrypt actif, **Force SSL** ✓

Sauvegarder, attendre 10 s, tester : https://tutorsphere.gisebs.com

### 2. Corriger NPM — API

**Proxy Host → `api.tutorsphere.gisebs.com`**

| Champ | Valeur |
|-------|--------|
| **Forward Hostname / IP** | `172.17.0.1` |
| **Forward Port** | **`55099`** (pas `5099`) |
| **Websockets Support** | ✓ (SignalR `/hubs/`) |

### 3. Vérifier le DNS

Chez le registrar / Cloudflare :

| Enregistrement | Type | Valeur |
|----------------|------|--------|
| `tutorsphere.gisebs.com` | A | `51.79.53.197` |
| `api.tutorsphere.gisebs.com` | A | `51.79.53.197` |

Vérification locale (sans SSH) :

```bash
nslookup tutorsphere.gisebs.com
nslookup api.tutorsphere.gisebs.com
```

Les deux doivent résoudre vers `51.79.53.197`.

### 4. Lancer le déploiement GitHub Actions

1. GitHub → dépôt **TutorSphere** → **Actions**
2. Workflow **Deploy Production** → **Run workflow** (branche `main`)
3. Attendre la fin — l’étape **Deploy** doit être verte
4. Si échec, lire les logs (secrets manquants, build Docker, healthcheck)

Secrets obligatoires : voir [GITHUB-SECRETS.md](./GITHUB-SECRETS.md)

| Secret | Rôle |
|--------|------|
| `SSH_PRIVATE_KEY_UBUNTU1` ou `TUTORSPHERE_SSH_PRIVATE_KEY` | Déploiement SSH |
| `TUTORSPHERE_CONNECTION_STRING` | PostgreSQL |
| `TUTORSPHERE_JWT_KEY` | Auth JWT (≥ 32 car.) |
| `TUTORSPHERE_PAYGATEWAY_BASE_URL` | Paiements |
| `TUTORSPHERE_PAYGATEWAY_API_KEY` | Paiements |

Optionnel : `TUTORSPHERE_API_BASE_URL` = `https://api.tutorsphere.gisebs.com`

---

## Vérifications sur le serveur (SSH requis)

À exécuter par un administrateur avec accès à `ubuntu@51.79.53.197` :

```bash
ssh ubuntu@51.79.53.197

# Santé des apps (doit renvoyer Healthy / HTTP 200)
curl -s http://127.0.0.1:55010/health
curl -s http://127.0.0.1:55099/health

# Même test que NPM (passerelle Docker → hôte)
curl -s http://172.17.0.1:55010/health
curl -s http://172.17.0.1:55099/health

# Conteneurs
cd /opt/apps/tutorsphere/app
docker compose -f docker-compose.yml -f docker-compose.prod.yml ps

# Logs récents
docker compose -f docker-compose.yml -f docker-compose.prod.yml logs --tail=80 api
docker compose -f docker-compose.yml -f docker-compose.prod.yml logs --tail=80 web
```

**Interprétation :**

| Résultat | Signification |
|----------|---------------|
| `127.0.0.1` OK, `172.17.0.1` KO | App liée à loopback seulement → redéployer (compose prod écoute `0.0.0.0`) |
| Les deux KO | Conteneurs arrêtés ou crash (DB, secrets, build) |
| Les deux OK, site 502 | NPM mal configuré (port `5010` / mauvais host) |

Redémarrage manuel si besoin :

```bash
cd /opt/apps/tutorsphere/app
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

---

## Causes courantes

| Symptôme / cause | Solution |
|------------------|----------|
| NPM Forward Port = **5010** ou **5099** | Ports **dev** (`docker-compose.yml`) — prod = **55010** / **55099** |
| NPM Forward Host incorrect | Utiliser **`172.17.0.1`** (NPM en conteneur Docker) |
| Secrets GitHub manquants | Workflow échoue à « Load configuration » — compléter [GITHUB-SECRETS.md](./GITHUB-SECRETS.md) |
| PostgreSQL inaccessible | Vérifier `TUTORSPHERE_CONNECTION_STRING`, DB `TutorSphere` sur `localhost:5432` |
| Conteneurs jamais buildés | Lancer **Deploy Production** ; vérifier `docker ps` |
| JWT / PayGateway invalides | Logs API au démarrage ; corriger secrets |
| Websockets désactivés dans NPM | Activer pour Blazor + SignalR |
| Déploiement jamais exécuté | Push sur `main` ou **Run workflow** manuel |

---

## Sans accès SSH — ce que vous devez faire

1. **Corriger NPM** (étapes 1 et 2) — accessible via l’interface web NPM (souvent `http://51.79.53.197:81` ou domaine admin GISEBS).
2. **Vérifier DNS** (étape 3).
3. **Relancer Deploy Production** sur GitHub (étape 4).
4. **Demander à un admin serveur** d’exécuter les commandes SSH ci-dessus si le 502 persiste après NPM + déploiement OK.
5. **Partager les logs GitHub Actions** (job **Deploy**, étape **Container status**) en cas d’échec du workflow.

---

## Références

- [deploy/nginx/NPM.md](./nginx/NPM.md) — configuration NPM détaillée
- [GITHUB-SECRETS.md](./GITHUB-SECRETS.md) — secrets et variables
- [README.md](./README.md) — vue d’ensemble déploiement
