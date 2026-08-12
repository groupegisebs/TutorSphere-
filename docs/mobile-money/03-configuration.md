# 03 — Configuration Mobile Money

## appsettings (GiseBsPayGateway)

```json
{
  "MobileMoney": {
    "DefaultProvider": "CamPay",
    "Country": "CM",
    "Currency": "XAF",
    "ChargeExpiryMinutes": 15,
    "Providers": {
      "CamPay": {
        "Enabled": true,
        "Environment": "Local",
        "BaseUrl": "",
        "WebhookSecretReference": "MobileMoney:CamPay:WebhookSecret",
        "UsernameReference": "MobileMoney:CamPay:Username",
        "PasswordReference": "MobileMoney:CamPay:Password"
      },
      "OrangeDirect": {
        "Enabled": false,
        "Environment": "Sandbox",
        "BaseUrl": "",
        "MerchantCode": "",
        "SecretReference": ""
      },
      "MtnDirect": {
        "Enabled": false,
        "Environment": "Sandbox",
        "BaseUrl": "",
        "SecretReference": ""
      }
    }
  }
}
```

### Environnements CamPay

| Environment | Comportement |
|-------------|--------------|
| `Local` | `LocalSimulatedMobileMoneyGateway` — aucun appel réseau |
| `Sandbox` | API CamPay démo (`https://demo.campay.net/api`) — nécessite secrets |
| `Production` | API CamPay prod (`https://campay.net/api`) — nécessite secrets + validation juridique |

## secrets.example.json (placeholders)

```json
{
  "MobileMoney": {
    "CamPay": {
      "Username": "PLACEHOLDER_CAMPAY_USERNAME",
      "Password": "PLACEHOLDER_CAMPAY_PASSWORD",
      "WebhookSecret": "PLACEHOLDER_CAMPAY_WEBHOOK_SECRET"
    }
  }
}
```

**Ne jamais committer de vraies clés.**

## Endpoints CamPay utilisés (documentation publique)

| Méthode | Chemin | Usage |
|---------|--------|-------|
| POST | `/token/` | Obtenir un jeton |
| POST | `/collect/` | Initier un paiement (idempotent via `external_reference`) |
| GET | `/transaction/{reference}/` | Consulter le statut |

## Activation sandbox → production

1. Obtenir compte entreprise CamPay + KYC.
2. Remplir `secrets.json` (Username/Password/WebhookSecret).
3. Passer `Environment` à `Sandbox`, tester T01–T12.
4. Validation juridique du modèle marchand (CDC §4.1 / §23).
5. Passer `Environment` à `Production`.
6. Feature flag : `Providers:CamPay:Enabled` pour coupure d’urgence.

## TutorSphere

Aucun secret CamPay côté TutorSphere. Configuration existante `PayGateway` (BaseUrl, AppCode, ApiKey) suffit.

Offres / abonnements doivent être en **XAF** pour afficher Orange/MTN dans le checkout.

## Taxes Afrique (TTC obligatoire)

- Catalogue : `GiseBsPayGateway` → `Services/Tax/AfricanTaxRates.cs` (tous les pays d’Afrique, taux standard TVA/GST/VAT).
- Calcul : `TTC = HT + taxe` (arrondi monétaire AwayFromZero ; XAF sans décimales).
- Exemple Cameroun : HT 5 000 XAF × 19,25 % → taxe 963 → **TTC 5 963**.
- APIs Gateway : `GET /api/tax/africa/rates`, `POST /api/tax/africa/quote`.
- APIs TutorSphere : `GET /api/payments/tax/africa/rates`, `GET /api/payments/tax/africa/quote`.
- Checkout MM : le payeur choisit son pays ; le montant encaissé est toujours le **TTC**. La commission plateforme reste calculée sur le **HT**.
- Défaut pays : `CM` (Cameroun) pour CamPay.

Les taux sont des standards publiés — à confirmer juridiquement avant production.
