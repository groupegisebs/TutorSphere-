# 02 — Architecture Mobile Money

## Vue d’ensemble

```text
TutorSphere (Billing / Checkout UI)
        │  X-App-Code + X-Api-Key
        ▼
GiseBsPayGateway
  MobileMoneyController  →  MobileMoneyOrchestrator
                                │
                    ┌───────────┼───────────┐
                    ▼           ▼           ▼
              Orange WebPay  MTN MoMo   CamPay (legacy off)
                 (CM)      Collections
                    │           │
              LocalSimulated si Environment=Local
```

Stripe reste derrière `IPaymentService` / `IStripeService` — séparé.

## Contrat `IMobileMoneyGateway`

- `ProviderCode`
- `InitiateAsync`
- `GetStatusAsync`
- `RefundAsync` → `NotSupported` tant que non confirmé
- `ValidateWebhookAsync` / `ParseWebhookAsync`
- `GetHealthAsync`

## Routage (CDC §5.1)

| Condition | Connecteur |
|-----------|------------|
| CM + Orange | Orange Money WebPay CM (ou Local) |
| CM + MTN | MTN MoMo Collections (ou Local) |
| Cartes / international | Stripe |
| CamPay Enabled + DefaultProvider=CamPay | Legacy agrégateur |

Ne jamais rerouter une tentative déjà initiée vers un autre fournisseur sous la même référence.

## Machine d’état

États ajoutés (valeurs numériques en fin d’enum pour compatibilité) :

- `PendingCustomerConfirmation`, `Expired`, `RefundPending`, `PartiallyRefunded`, `RequiresReview`

Règles :

- `Succeeded` est terminal pour le paiement ; correction via remboursement / ajustement.
- Événement incohérent (montant, devise) → `RequiresReview`.
- Transitions centralisées dans `MobileMoneyStateMachine`.

## Idempotence

1. Header `Idempotency-Key` sur `POST /api/mobile-money/charge`.
2. Index unique `PaymentTransaction.IdempotencyKey` (scoped app).
3. CamPay `external_reference` = référence interne.
4. `MobileMoneyWebhookEvent.PayloadHash` / `ProviderEventId` uniques.

## Webhooks

- `POST /api/webhooks/campay` — authentifié (secret configurable), anti-rejeu, traitement synchrone DB pour le MVP (file durable/outbox = lot ultérieur).
- `/orange`, `/mtn` → 501 tant que non activés.
- Bypass API key (comme Stripe).

## Sécurité

- Pas de PIN/OTP stockés.
- Numéro masqué uniquement en base (`PhoneMasked`).
- Secrets CamPay dans `secrets.json` uniquement.
- Montant toujours recalculé côté serveur (TutorSphere + gateway catalogue).
