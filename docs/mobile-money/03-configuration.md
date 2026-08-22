# 03 — Configuration Mobile Money

## Architecture active

- **MTN** → API MoMo Collections (`requesttopay`) directe
- **Orange** → API Web Payment Cameroun (redirection `payment_url`)
- **CamPay** → désactivé par défaut (legacy)

## appsettings (GiseBsPayGateway)

```json
{
  "MobileMoney": {
    "DefaultProvider": "Direct",
    "Country": "CM",
    "Currency": "XAF",
    "ChargeExpiryMinutes": 15,
    "Providers": {
      "CamPay": { "Enabled": false, "Environment": "Local" },
      "OrangeDirect": {
        "Enabled": true,
        "Environment": "Local",
        "WebPaymentPath": "orange-money-webpay/cm/v1/webpayment",
        "ReturnUrl": "",
        "CancelUrl": "",
        "NotifUrl": ""
      },
      "MtnDirect": {
        "Enabled": true,
        "Environment": "Local",
        "TargetEnvironment": "",
        "CallbackUrl": ""
      }
    }
  }
}
```

### Environnements

| Environment | Effet |
|-------------|--------|
| `Local` | Simulateur interne (aucun appel réseau) |
| `Sandbox` | APIs sandbox Orange / MTN — secrets requis |
| `Production` | APIs prod — secrets + KYC marchand |

MTN prod : `X-Target-Environment: mtncameroon`  
Orange prod : `https://api.orange.com/orange-money-webpay/cm/v1/webpayment`

## Produit MTN à souscrire

Sur le portail [momodeveloper](https://momodeveloper.mtn.com) : **Collections** (pas Collection Widget, pas Remittances).

Disbursements = versements enseignants (POST `/transfer`) — produit séparé, clé d’abonnement distincte. TutorSphere encaisse d’abord via Collections (`POST /collection/v1_0/requesttopay`).

### Identifiants (deux couches)

| Secret | Rôle |
|--------|------|
| Primary / Secondary Key | Header `Ocp-Apim-Subscription-Key` (API Manager) |
| API User + API Key | OAuth 2.0 Client Credentials → Bearer (`POST /collection/token/`) |

Sandbox : Provisioning `POST /v1_0/apiuser` puis `POST /v1_0/apiuser/{id}/apikey`.  
Production : Partner Portal du pays (`X-Target-Environment: mtncameroon`). Le callback host doit être public : `{PublicBaseUrl}/api/webhooks/mtn`.

### Erreurs fréquentes (Open API)

| Code | Action côté GISE |
|------|------------------|
| `RESOURCE_ALREADY_EXIST` (409) | Même UUID v4 déjà envoyé → lecture du statut |
| `ACCESS_DENIED` (401) | Mauvaise clé produit → essai **Secondary Key** Collections |
| `RESOURCE_NOT_FOUND` (404) | GET trop tôt ou RTP non 202 → rester en attente |
| `PAYER_NOT_FOUND` | MSISDN avec indicatif `237`, compte MoMo actif |
| `INTERNAL_PROCESSING_ERROR` | Souvent solde insuffisant |
| `COULD_NOT_PERFORM_TRANSACTION` | Parent n’a pas approuvé sous **5 minutes** |
| `NOT_ALLOWED_TARGET_ENVIRONMENT` | `sandbox` ou `mtncameroon` |
| `INVALID_CALLBACK_URL_HOST` | Hostname du callback = celui de l’API User (pas une IP) |
| `/token` + body | **400** — le jeton s’envoie **sans corps** |

## secrets.json (serveur)

```json
{
  "MobileMoney": {
    "Orange": {
      "ClientId": "...",
      "ClientSecret": "...",
      "AuthorizationHeader": "",
      "MerchantKey": "..."
    },
    "Mtn": {
      "SubscriptionKey": "...",
      "SecondaryKey": "...",
      "ApiUserId": "...",
      "ApiKey": "..."
    }
  }
}
```

Fichier : `/opt/apps/gisebs-pay-gateway/secrets.json` (`chmod 600`). Ne jamais committer.

## Webhooks

| Provider | URL |
|----------|-----|
| Orange | `{PublicBaseUrl}/api/webhooks/orange` |
| MTN | `{PublicBaseUrl}/api/webhooks/mtn` |

Configurer ces URLs dans les consoles Orange (notif_url) et MTN (callback host / X-Callback-Url).

## Activation sandbox → production

1. Comptes marchands **Orange Money WebPay CM** + **MTN MoMo Collections**.
2. Remplir `secrets.json`.
3. Passer `OrangeDirect` / `MtnDirect` `Environment` à `Sandbox`, tester.
4. Validation juridique / KYC.
5. Passer à `Production` + `TargetEnvironment=mtncameroon` pour MTN.

## TutorSphere

Aucun secret MM côté TutorSphere (`PayGateway` suffit).  
Offres en **XAF**.  
Orange → redirection WebPay ; MTN → saisie téléphone + push USSD.

## Taxes Afrique

Voir section admin `/Admin/TaxRates`. Cameroun éducation = 0 % par défaut.
