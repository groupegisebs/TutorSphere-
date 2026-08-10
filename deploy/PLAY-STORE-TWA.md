# Publier TutorSphere sur le Play Store (TWA / PWABuilder)

Guide pratique pour empaqueter la PWA TutorSphere en **Trusted Web Activity (TWA)** Android et la publier sur Google Play.

URL de prod : `https://tutorsphere.gisebs.com`

---

## 1. Prérequis

| Élément | Détail |
|---------|--------|
| **Compte Google Play Console** | Compte développeur payant, app créée (ou prête à créer) |
| **HTTPS** | Site servi en HTTPS valide (déjà via NPM + Let’s Encrypt) |
| **PWA prête** | `manifest.webmanifest`, `service-worker.js`, icônes, installable sur Chrome Android |
| **Digital Asset Links** | Fichier `/.well-known/assetlinks.json` déployé avec le bon package + SHA-256 |
| **Cloudflare** | Ne **pas** challenger le SW, le manifest ni `assetlinks.json` (voir [nginx/NPM.md](nginx/NPM.md) § Cloudflare) |

Sans `assetlinks.json` correct, Chrome n’affiche pas la TWA en plein écran (barre d’URL visible) et la validation Play peut échouer.

---

## 2. Générer le package Android avec PWABuilder

1. Ouvrir [PWABuilder](https://www.pwabuilder.com).
2. Entrer l’URL : `https://tutorsphere.gisebs.com`.
3. Lancer le scan ; corriger les alertes bloquantes (manifest, icônes, SW) si besoin.
4. Choisir **Package for stores** → **Android** → package type **TWA** (Trusted Web Activity).
5. Renseigner notamment :
   - **Package ID** : `com.gisebs.tutorsphere` (doit correspondre à `assetlinks.json` ; changeable si vous préférez un autre ID)
   - Nom d’affichage, couleurs, icône (PWABuilder peut dériver du manifest)
6. Générer / télécharger le projet ou le **AAB** (Android App Bundle).

Gardez le même `package_name` partout : Play Console, PWABuilder, et `assetlinks.json`.

---

## 3. Récupérer le SHA-256 (Play App Signing)

Google signe l’app avec sa clé **Play App Signing**. C’est **cette** empreinte (pas seulement votre keystore local de upload) qu’il faut mettre dans `assetlinks.json`.

1. [Play Console](https://play.google.com/console) → votre app TutorSphere.
2. **Configuration** (Setup) → **Intégrité de l'application** / **App integrity** → **Signature de l'application** (App signing).
3. Copier l’empreinte **SHA-256** du **certificat de signature d’application** (App signing key certificate).
4. Format attendu : hexadécimal avec deux-points, ex. `AB:CD:12:...` (comme affiché dans la console).

Si vous testez encore avec un APK/AAB signé localement (avant Play App Signing), vous pouvez temporairement ajouter aussi le SHA-256 de votre keystore d’upload — en prod, priorisez celui de **Play App Signing**.

---

## 4. Compléter et déployer `assetlinks.json`

Fichier source (template) :

`src/TutorSphere.Web/wwwroot/.well-known/assetlinks.json`

Exemple une fois rempli :

```json
[
  {
    "relation": ["delegate_permission/common.handle_all_urls"],
    "target": {
      "namespace": "android_app",
      "package_name": "com.gisebs.tutorsphere",
      "sha256_cert_fingerprints": [
        "AB:CD:EF:...empreinte Play App Signing..."
      ]
    }
  }
]
```

1. Remplacer `REPLACE_WITH_PLAY_APP_SIGNING_SHA256` par l’empreinte SHA-256.
2. Vérifier que `package_name` = ID Android Play / PWABuilder (`com.gisebs.tutorsphere` par défaut).
3. Commit + déploiement prod (push `main` / Deploy Production).
4. Vérifier :

```bash
curl -sI https://tutorsphere.gisebs.com/.well-known/assetlinks.json
# → HTTP 200, Content-Type: application/json

curl -s https://tutorsphere.gisebs.com/.well-known/assetlinks.json
```

Outil Google (optionnel) :  
`https://digitalassetlinks.googleapis.com/v1/statements:list?source.web.site=https://tutorsphere.gisebs.com&relation=delegate_permission/common.handle_all_urls`

---

## 5. Upload Play Console et fiche store

1. Play Console → **Production** (ou piste **Internal testing** / **Closed** d’abord) → créer une version.
2. Uploader l’**AAB** généré par PWABuilder.
3. Compléter la fiche store (minimum) :
   - Titre, description courte / longue
   - Icône 512×512, feature graphic, captures d’écran téléphone
   - Catégorie, contact, politique de confidentialité (URL HTTPS)
   - Questionnaire contenu / public cible
4. Soumettre pour **examen** (review). Prévoir un délai Google (souvent quelques jours pour une première app).

Conseil : valider d’abord en **test interne** avec le même `assetlinks.json` en prod, pour confirmer l’affichage plein écran TWA.

---

## 6. Limites importantes (TutorSphere)

| Point | Impact |
|-------|--------|
| **Blazor Server** | L’app a besoin du réseau ; ce n’est **pas** une app native offline |
| **Circuit SignalR** | Coupures data / challenges Cloudflare cassent l’UI |
| **Cloudflare** | Managed Challenge / Bot Fight sur mobile LTE cassent SW, install et TWA — voir [NPM.md § Cloudflare](nginx/NPM.md) |
| **assetlinks** | Doit rester accessible sans challenge ni auth (`200` + `application/json`) |
| **PWA ≠ APK offline** | Le service worker aide au shell / offline.html ; le cœur métier reste serveur |

---

## 7. Cloudflare — chemins à ne pas challenger

En plus de `/_blazor`, inclure dans une règle WAF **Skip** (voir détail dans [nginx/NPM.md](nginx/NPM.md)) :

- `/service-worker.js`
- `/manifest.webmanifest`
- `/.well-known/assetlinks.json`

Sans cela, Google et les appareils Android peuvent échouer à vérifier le lien site ↔ app.

---

## Références

- [PWABuilder](https://www.pwabuilder.com)
- Template Asset Links : `src/TutorSphere.Web/wwwroot/.well-known/assetlinks.json`
- Reverse proxy / Cloudflare : [nginx/NPM.md](nginx/NPM.md)
- Déploiement général : [README.md](README.md)
