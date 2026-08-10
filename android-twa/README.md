# TutorSphere — projet Android TWA (Trusted Web Activity)

Package : `com.gisebs.tutorsphere`  
URL : `https://tutorsphere.gisebs.com`  
Ouvrir ce dossier dans **Android Studio**.

Ce projet enveloppe la PWA existante en application Android pour le **Play Store** (AAB).

---

## Prérequis

- Android Studio (JDK embarqué JBR suffit)
- Compte [Google Play Console](https://play.google.com/console)
- Site en HTTPS + `/.well-known/assetlinks.json` déployé (voir `../deploy/PLAY-STORE-TWA.md`)

---

## Générer l’AAB dans Android Studio (recommandé)

1. **File → Open** → sélectionner `TutorSphere-/android-twa/`
2. Attendre la sync Gradle
3. **Build → Generate Signed Bundle / APK…**
4. Choisir **Android App Bundle**
5. Créer ou sélectionner un **keystore** d’upload (voir ci-dessous)
6. Build type : **release**
7. Récupérer le fichier : `app/release/app-release.aab` (ou le chemin indiqué par Studio)

Ensuite : Play Console → créer / ouvrir l’app → uploader l’AAB (piste Internal testing d’abord).

---

## Keystore (signature d’upload)

### Créer un keystore (une seule fois)

Dans un terminal (JDK Android Studio) :

```bat
set JAVA_HOME=C:\Program Files\Android\Android Studio\jbr
"%JAVA_HOME%\bin\keytool" -genkeypair -v -keystore upload-keystore.jks -keyalg RSA -keysize 2048 -validity 10000 -alias tutorsphere
```

Placez `upload-keystore.jks` dans `android-twa/` (ignoré par git).

### Option CLI (signature via Gradle)

1. Copier `keystore.properties.example` → `keystore.properties`
2. Remplir mots de passe / alias / chemin
3. `gradlew.bat bundleRelease`

### Play App Signing

Activez **Play App Signing** dans la console. Google garde la clé de signature d’application ; vous uploadez avec le keystore d’upload.  
L’empreinte **SHA-256 à mettre dans `assetlinks.json`** est celle du **certificat de signature d’application** (App signing), pas forcément celle du keystore d’upload.

---

## SHA-256 → assetlinks.json

1. Play Console → app → **Configuration / Setup** → **Intégrité / App integrity** → **Signature de l’application**
2. Copier le **SHA-256** du certificat **App signing**
3. Remplacer `REPLACE_WITH_PLAY_APP_SIGNING_SHA256` dans :

   `src/TutorSphere.Web/wwwroot/.well-known/assetlinks.json`

4. Déployer en prod, puis vérifier :

```bat
curl -s https://tutorsphere.gisebs.com/.well-known/assetlinks.json
```

Empreinte locale (keystore d’upload, utile pour tests hors Play) :

```bat
"%JAVA_HOME%\bin\keytool" -list -v -keystore upload-keystore.jks -alias tutorsphere
```

Cherchez la ligne **SHA256:**.

---

## Build CLI (sans Android Studio UI)

```bat
cd TutorSphere-\android-twa
set JAVA_HOME=C:\Program Files\Android\Android Studio\jbr
copy local.properties.example local.properties
REM éditer sdk.dir dans local.properties
gradlew.bat bundleRelease
```

Sans `keystore.properties`, le bundle release est **non signé** (ou signé debug selon config) — pour le Play Store, préférez **Generate Signed Bundle** dans Studio ou `keystore.properties`.

Artefact attendu : `app\build\outputs\bundle\release\app-release.aab`

---

## Identifiants à garder alignés

| Lieu | Valeur |
|------|--------|
| `applicationId` / Play Console | `com.gisebs.tutorsphere` |
| `assetlinks.json` → `package_name` | `com.gisebs.tutorsphere` |
| Host TWA | `tutorsphere.gisebs.com` |

---

## Fichiers secrets (ne jamais committer)

- `*.jks` / `*.keystore`
- `keystore.properties`
- `local.properties`

Voir le `.gitignore` du dépôt et celui de ce dossier.
