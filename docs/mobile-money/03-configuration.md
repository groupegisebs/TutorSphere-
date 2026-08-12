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
