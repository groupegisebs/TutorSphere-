# Configuration Nginx Proxy Manager — TutorSphere

TutorSphere écoute sur l’**hôte Linux** (`network_mode: host`), pas sur le réseau bridge Docker.

| Service | Port hôte | Usage |
|---------|-----------|--------|
| Web (Blazor) | **55010** | Interface utilisateur |
| API (ASP.NET) | **55099** | REST + SignalR (`/hubs/`) |

> **Ne pas utiliser** les ports de dev Docker (`5010` / `5099`) — ce sont les mappings locaux de `docker-compose.yml`.

NPM tourne dans un conteneur Docker. Depuis NPM, l’hôte est joignable via **`172.17.0.1`** (passerelle bridge Docker), comme les autres apps GISEBS (ex. giseboutique `:5001`, comptadoc `:5050`).

---

## 1. DNS (chez le registrar / Cloudflare)

| Enregistrement | Type | Valeur | Statut |
|----------------|------|--------|--------|
| `tutorsphere.gisebs.com` | A | `51.79.53.197` | requis (Web) |
| `api.tutorsphere.gisebs.com` | A | `51.79.53.197` | **requis (API)** — sans cet enregistrement, `https://api.tutorsphere.gisebs.com` ne résout pas |

> Blazor Server appelle l’API via `INTERNALAPIBASEURL=http://127.0.0.1:55099` sur le serveur. Le sous-domaine `api.*` reste nécessaire pour les appels **navigateur** directs (SignalR client futur, outils, intégrations).

---

## 2. Proxy Host — Web (Blazor)

**Hosts → Proxy Hosts → Add Proxy Host**

| Champ | Valeur |
|-------|--------|
| **Domain Names** | `tutorsphere.gisebs.com` |
| **Scheme** | `http` |
| **Forward Hostname / IP** | `172.17.0.1` |
| **Forward Port** | **`55010`** |
| **Block Common Exploits** | ✓ |
| **Websockets Support** | ✓ (Blazor Server + circuits `/_blazor`) |

**SSL** : Request a new SSL Certificate (Let's Encrypt), Force SSL ✓

**Advanced** (optionnel, custom locations) :

```nginx
location /_blazor {
    proxy_pass http://172.17.0.1:55010;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_read_timeout 86400;
}
```

---

## 3. Proxy Host — API

| Champ | Valeur |
|-------|--------|
| **Domain Names** | `api.tutorsphere.gisebs.com` |
| **Scheme** | `http` |
| **Forward Hostname / IP** | `172.17.0.1` |
| **Forward Port** | **`55099`** |
| **Block Common Exploits** | ✓ |
| **Websockets Support** | ✓ (SignalR `/hubs/messages`) |

**SSL** : Request a new SSL Certificate, Force SSL ✓

**Advanced** (SignalR) :

```nginx
location /hubs/ {
    proxy_pass http://172.17.0.1:55099;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_read_timeout 86400;
}
```

---

## 4. Secret GitHub `TUTORSPHERE_API_BASE_URL`

Le navigateur appelle l’API via HTTPS public. Dans GitHub (secret ou variable) :

```
TUTORSPHERE_API_BASE_URL=https://api.tutorsphere.gisebs.com
```

Sans ce secret, le déploiement utilise cette URL par défaut (`deploy/build-app-env.sh`).

---

## 5. Vérification sur le serveur

```bash
ssh ubuntu@51.79.53.197
curl -s http://127.0.0.1:55010/health   # → Healthy
curl -s http://127.0.0.1:55099/health   # → Healthy
curl -sI https://tutorsphere.gisebs.com
curl -sI https://api.tutorsphere.gisebs.com/health
```

---

## Dépannage 502 Bad Gateway

Guide complet : [TROUBLESHOOTING-502.md](../TROUBLESHOOTING-502.md)

| Cause | Correction |
|-------|------------|
| NPM pointe vers `:5010` (port dev) | Changer Forward Port → **`55010`** |
| App écoute `127.0.0.1` seulement | Redéployer — prod écoute **`0.0.0.0:55010`** / **`:55099`** |
| Conteneurs arrêtés | `cd /opt/apps/tutorsphere/app && docker compose -f docker-compose.yml -f docker-compose.prod.yml ps` |
| Déploiement jamais exécuté | Lancer **Deploy Production** dans GitHub Actions |
| `172.17.0.1` inaccessible depuis NPM | Essayer **`127.0.0.1`** si NPM n’est pas conteneurisé |

Alternative si NPM et TutorSphere partagent le même hôte sans bridge : Forward Host **`127.0.0.1`**, ports **`55010`** / **`55099`**.
