# Publier TutorSphere sur le Play Store (TWA)

Guide pratique pour empaqueter la PWA TutorSphere en **Trusted Web Activity (TWA)** Android et la publier sur Google Play.

URL de prod : `https://tutorsphere.gisebs.com`  
Package Android : `com.gisebs.tutorsphere`

Projet Android dans le dépôt : [`../android-twa/`](../android-twa/) (ouvrir dans Android Studio).

---

## 1. Prérequis

| Élément | Détail |
|---------|--------|
| **Compte Google Play Console** | Compte développeur payant, app créée (ou prête à créer) |
| **Android Studio** | Pour ouvrir `android-twa/` et générer l’AAB signé |
| **HTTPS** | Site servi en HTTPS valide (déjà via NPM + Let’s Encrypt) |
| **PWA prête** | `manifest.webmanifest`, `service-worker.js`, icônes, installable sur Chrome Android |
| **Digital Asset Links** | Fichier `/.well-known/assetlinks.json` déployé avec le bon package + SHA-256 |
| **Cloudflare** | Ne **pas** challenger le SW, le manifest ni `assetlinks.json` (voir [nginx/NPM.md](nginx/NPM.md) § Cloudflare) |

Sans `assetlinks.json` correct, Chrome n’affiche pas la TWA en plein écran (barre d’URL visible) et la validation Play peut échouer.

---

## 2. Chemin recommandé — Android Studio + projet `android-twa`

### 2.1 Ouvrir le projet

1. Lancer **Android Studio**.
2. **File → Open…** → sélectionner le dossier  
   `TutorSphere-/android-twa/`
3. Laisser Gradle synchroniser (JDK JBR d’Android Studio suffit).

Détails keystore / CLI : [`../android-twa/README.md`](../android-twa/README.md).

### 2.2 Créer le keystore d’upload (une seule fois)

Si vous n’avez pas encore de keystore :

```bat
set JAVA_HOME=C:\Program Files\Android\Android Studio\jbr
"%JAVA_HOME%\bin\keytool" -genkeypair -v -keystore upload-keystore.jks -keyalg RSA -keysize 2048 -validity 10000 -alias tutorsphere
```

Conservez le fichier et les mots de passe en lieu sûr. **Ne pas committer** `*.jks` / `*.keystore`.

Sur Play Console, activez **Play App Signing** : Google signe l’app pour les utilisateurs ; vous uploadez avec ce keystore d’upload.

### 2.3 Générer l’AAB (Android App Bundle)

1. **Build → Generate Signed Bundle / APK…**
2. Choisir **Android App Bundle** → Next
3. Sélectionner (ou créer) le keystore → alias `tutorsphere` → Next
4. Build variant : **release** → Create
5. Noter le chemin de `app-release.aab` (souvent sous `android-twa/app/release/` ou `app/build/outputs/bundle/release/`)

### 2.4 Upload Play Console

1. [Play Console](https://play.google.com/console) → créer l’app **TutorSphere** si besoin (package `com.gisebs.tutorsphere`).
2. Piste **Internal testing** (recommandé d’abord) → créer une version → uploader l’AAB.
3. Compléter la fiche store (voir § 6).

### 2.5 Aligner package + assetlinks

Après le premier upload (Play App Signing actif) :

1. Récupérer le SHA-256 **App signing** (§ 4).
2. Mettre à jour `assetlinks.json` et déployer en prod.
3. Vérifier le plein écran TWA sur un appareil de test.

---

## 3. Alternative — PWABuilder (sans projet local)

1. Ouvrir [PWABuilder](https://www.pwabuilder.com).
2. Entrer `https://tutorsphere.gisebs.com` → scanner.
3. **Package for stores** → **Android** → type **TWA**.
4. **Package ID** : `com.gisebs.tutorsphere` (identique à `assetlinks.json`).
5. Générer / télécharger l’**AAB** (ou le projet), puis uploader comme ci-dessus.

Gardez le même `package_name` partout : Play Console, PWABuilder / `android-twa`, et `assetlinks.json`.

---

## 4. Récupérer le SHA-256 (Play App Signing)

Google signe l’app avec sa clé **Play App Signing**. C’est **cette** empreinte (pas seulement votre keystore local d’upload) qu’il faut mettre dans `assetlinks.json`.

1. [Play Console](https://play.google.com/console) → votre app TutorSphere.
2. **Configuration** (Setup) → **Intégrité de l'application** / **App integrity** → **Signature de l'application** (App signing).
3. Copier l’empreinte **SHA-256** du **certificat de signature d’application** (App signing key certificate).
4. Format attendu : hexadécimal avec deux-points, ex. `AB:CD:12:...` (comme affiché dans la console).

Si vous testez encore avec un APK/AAB signé localement (avant Play App Signing), vous pouvez temporairement ajouter aussi le SHA-256 de votre keystore d’upload — en prod, priorisez celui de **Play App Signing**.

Empreinte du keystore local :

```bat
"%JAVA_HOME%\bin\keytool" -list -v -keystore upload-keystore.jks -alias tutorsphere
```

---

## 5. Compléter et déployer `assetlinks.json`

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
2. Vérifier que `package_name` = ID Android Play (`com.gisebs.tutorsphere`).
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

## 6. Fiche store Play Console

Minimum à prévoir :

- Titre, description courte / longue
- Icône 512×512, feature graphic, captures d’écran téléphone
- Catégorie, contact, politique de confidentialité (URL HTTPS)
- Questionnaire contenu / public cible
- Soumettre pour **examen** (souvent quelques jours pour une première app)

Conseil : valider d’abord en **test interne** avec le même `assetlinks.json` en prod, pour confirmer l’affichage plein écran TWA.

---

## 7. Limites importantes (TutorSphere)

| Point | Impact |
|-------|--------|
| **Blazor Server** | L’app a besoin du réseau ; ce n’est **pas** une app native offline |
| **Circuit SignalR** | Coupures data / challenges Cloudflare cassent l’UI |
| **Cloudflare** | Managed Challenge / Bot Fight sur mobile LTE cassent SW, install et TWA — voir [NPM.md § Cloudflare](nginx/NPM.md) |
| **assetlinks** | Doit rester accessible sans challenge ni auth (`200` + `application/json`) |
| **PWA ≠ APK offline** | Le service worker aide au shell / offline.html ; le cœur métier reste serveur |

---

## 8. Cloudflare — chemins à ne pas challenger

En plus de `/_blazor`, inclure dans une règle WAF **Skip** (voir détail dans [nginx/NPM.md](nginx/NPM.md)) :

- `/service-worker.js`
- `/manifest.webmanifest`
- `/.well-known/assetlinks.json`

Sans cela, Google et les appareils Android peuvent échouer à vérifier le lien site ↔ app.

---

## Références

- Projet Android TWA : [`../android-twa/README.md`](../android-twa/README.md)
- [PWABuilder](https://www.pwabuilder.com) (alternative)
- Template Asset Links : `src/TutorSphere.Web/wwwroot/.well-known/assetlinks.json`
- Reverse proxy / Cloudflare : [nginx/NPM.md](nginx/NPM.md)
- Déploiement général : [README.md](README.md)
