# Notifications WhatsApp

Canal additionnel aux courriels, pour les rappels de cours envoyés aux parents. Le courriel reste
la trace écrite et n'est jamais remplacé : WhatsApp s'ajoute quand le parent l'a demandé.

## Principe

```
LessonReminderService (H-24)
  ├─ IEmailService      → POST /api/mail/send      (inchangé)
  └─ IWhatsAppNotifier  → POST /api/whatsapp/send  (si le parent a un numéro vérifié)
```

La passerelle est la même (`GiseMailSender`), avec la même clé et le même code client : la section
de configuration `Email` sert aux deux canaux, aucun nouveau secret n'est nécessaire côté TutorSphere.

## Règles de sécurité appliquées

1. **Numéro vérifié obligatoire.** Un code à six chiffres est envoyé sur WhatsApp et doit être saisi
   dans l'application. Tant qu'il ne l'est pas, aucun message métier ne part. Un numéro périmé ou
   réattribué exposerait sinon des informations sur un enfant mineur.
2. **Le code n'est jamais stocké en clair** : seule une empreinte PBKDF2 salée est conservée, avec
   une durée de vie de dix minutes, cinq tentatives et un délai entre deux demandes.
3. **Consentement tracé** : date, origine et date de révocation restent en base même après
   désabonnement, pour pouvoir prouver l'accord.
4. **Aucune donnée sensible dans les messages** : ni note, ni montant, ni adresse, ni code d'accès.
   Le message annonce et renvoie vers l'application, qui exige une connexion.
5. **Un seul canal par compte** (index unique sur `UserId`) : le numéro notifié est sans ambiguïté.
6. **Le parent est le destinataire** des notifications concernant un élève mineur.

## Modèles à faire approuver chez Meta

Chaque code fonctionnel doit correspondre à un modèle approuvé, par langue. À créer dans
**WhatsApp Manager → Modèles de messages**.

### 1. Code de vérification — catégorie « Authentification »

Nom Meta suggéré : `tutorsphere_verification_code`

Corps : `Votre code de vérification TutorSphere est {{1}}. Il expire dans 10 minutes.`

### 2. Rappel de cours — catégorie « Utilitaire »

Nom Meta suggéré : `tutorsphere_lesson_reminder`

Corps : `Bonjour {{1}}, rappel : le cours de {{3}} avec {{2}} est prévu le {{4}}. Détails dans TutorSphere.`

## Déclarer les correspondances dans la passerelle

Sans ces correspondances, l'envoi est refusé avant tout appel à Meta. L'ordre des paramètres doit
suivre les `{{n}}` du modèle approuvé.

```bash
curl -X POST "https://gisemailsender.gisebs.com/api/whatsapp/templates" \
  -H "Authorization: Bearer $TUTORSPHERE_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "templateCode": "WHATSAPP_VERIFY_CODE",
    "language": "fr",
    "metaTemplateName": "tutorsphere_verification_code",
    "metaLanguageCode": "fr",
    "bodyParameters": ["Code"]
  }'

curl -X POST "https://gisemailsender.gisebs.com/api/whatsapp/templates" \
  -H "Authorization: Bearer $TUTORSPHERE_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "templateCode": "LESSON_REMINDER",
    "language": "fr",
    "metaTemplateName": "tutorsphere_lesson_reminder",
    "metaLanguageCode": "fr",
    "bodyParameters": ["RecipientName", "TutorName", "Subject", "LessonDate"]
  }'
```

À répéter par langue servie (`en`, `es`…) avec le modèle approuvé correspondant.

## Configuration TutorSphere

Section `WhatsApp`, toutes les valeurs ont un défaut utilisable :

| Clé | Défaut | Rôle |
|-----|--------|------|
| `WhatsApp:DefaultCountryCode` | `1` | Indicatif ajouté aux numéros saisis sans indicatif |
| `WhatsApp:CodeLifetimeMinutes` | `10` | Durée de vie du code de vérification |
| `WhatsApp:MaxVerificationAttempts` | `5` | Saisies erronées tolérées |
| `WhatsApp:ResendCooldownSeconds` | `60` | Délai entre deux demandes de code |

## Parcours parent

**Réglages → Rappels sur WhatsApp** : saisie du numéro, réception du code, confirmation. Ensuite un
interrupteur active ou coupe les rappels, et un bouton retire le numéro. Endpoints correspondants :

| Méthode | Route | Effet |
|---------|-------|-------|
| `GET` | `/api/me/whatsapp` | État du canal, numéro masqué |
| `POST` | `/api/me/whatsapp/start` | Envoie un code au numéro fourni |
| `POST` | `/api/me/whatsapp/confirm` | Active le canal si le code est bon |
| `PUT` | `/api/me/whatsapp/preferences` | Active ou coupe les rappels |
| `DELETE` | `/api/me/whatsapp` | Désabonnement |

## Prérequis côté Meta avant la mise en service

- Application Meta en **mode Live** : en mode Development, aucun webhook de production n'est livré,
  donc les statuts « remis » et « lu » n'arrivent jamais.
- **Numéro d'entreprise vérifié** : le numéro de test n'écrit qu'aux cinq numéros autorisés.
- **Moyen de paiement rattaché** : les messages initiés par l'entreprise sont facturés à l'unité.
