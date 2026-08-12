# 01 — Audit existant (Mobile Money Cameroun)

**Date :** 12 août 2026  
**Sources :** `Cahier_des_charges_Paiement_Mobile_Money_TutorSphere_v1.0.pdf`, dépôt TutorSphere + GiseBsPayGateway  
**Périmètre :** Lots 0/1/2 + tranche Lot 3 (CamPay via passerelle, simulateur local)

## 1. Architecture détectée

### TutorSphere (.NET 10, clean architecture)

| Projet | Rôle |
|--------|------|
| Domain | Entités `Payment`, `Invoice`, `StudentSubscription`, `TutorPayout`, `Tenant` |
| Application | `IPaymentGatewayService`, DTOs paiements |
| Infrastructure | EF Core + Npgsql, `PayGatewayClient` → GiseBsPayGateway |
| Api | JWT + Identity, `PaymentsController`, background reminders |
| Web | Blazor Server, `PaymentCheckoutModal.razor` (Card / PayPal) |

Multi-tenant : `ITenantEntity` + filtres EF globaux + `TenantResolutionMiddleware`.

### GiseBsPayGateway (.NET 10)

Passerelle centralisée multi-apps (`ClientApplication` + `X-App-Code` / `X-Api-Key`).

- **Collecte actuelle :** Stripe uniquement (`PaymentTransaction.Provider = "stripe"`).
- **Payout Mobile Money :** validation/enregistrement destinataires + file de disbursements (admin) — orthogonal à la collecte.
- Flutterwave / collecte MM a été retiré (migration `RemoveFlutterwave`).

## 2. Composants réutilisables

- Flux checkout TutorSphere → `PayGatewayClient` → Stripe Checkout.
- Entités `Payment` / `Invoice` / `StudentSubscription` et activation après `Succeeded`.
- `TutorPayout` / `TutorDisbursementGateway` pour reversements Afrique (Wave, Orange, MTN, M-Pesa).
- `PaymentTransaction.Provider` comme discriminateur de connecteur.
- Pattern webhook Stripe (`StripeWebhookEvent`, idempotence, signature).
- Secrets serveur (`secrets.json` + `SERVER-SECRETS.md`).
- Audit (`IAuditService`), rate limiting, health checks.
- `CatalogOptions.IsZeroDecimalCurrency` inclut déjà `xaf`.

## 3. Composants manquants (CDC)

| CDC | État |
|-----|------|
| `IMobileMoneyGateway` + CamPay / stubs Orange & MTN | À créer dans GiseBsPayGateway |
| Statuts `PendingCustomerConfirmation`, `Expired`, `RequiresReview`, … | À étendre sur `PaymentStatus` |
| Colonnes MM sur `PaymentTransaction` (channel, phone masked, provider ref, idempotency) | À ajouter |
| `MobileMoneyWebhookEvent` | À créer |
| Endpoints `/api/mobile-money/charge`, `/api/webhooks/campay` | À créer |
| UI parent Orange/MTN + numéro +237 | À ajouter (tranche minimale) |
| Rôle Finance + MFA | Reporté (réutiliser SuperAdmin/PlatformAdmin) |
| Ledger / Settlement / Reconciliation dédiés | Hors lot (réutiliser TutorPayout) |
| Entitlement séparé | Absente — activation via `StudentSubscription` |

## 4. Conflits avec le cahier des charges

1. CDC décrit tout dans TutorSphere ; décision actée : **connecteurs dans GiseBsPayGateway**, orchestration métier dans TutorSphere.
2. CDC exige rôle Finance + MFA : **reporté**.
3. Pas de clés CamPay réelles : **mode Local simulateur** + placeholders.
4. Pas de prélèvement silencieux : déjà aligné (confirmation parent à chaque renouvellement).
5. Devise XAF : catalogues TutorSphere souvent CAD — l’offre doit être en XAF pour activer MM.

## 5. Migrations nécessaires

**GiseBsPayGateway**

- Étendre `PaymentStatus`.
- Colonnes MM sur `PaymentTransactions`.
- Table `MobileMoneyWebhookEvents`.

**TutorSphere**

- `PaymentsSet` : `Channel`, `PhoneMasked` (nullable).

## 6. Risques techniques

| Risque | Mesure |
|--------|--------|
| Hypothèses CDC §23 ouvertes (entité juridique, KYC, modèle de fonds) | Pas d’encaissement réel tant que non validé |
| Signature webhook CamPay non confirmée contractuellement | Validation configurable + mode Local ; `RequiresReview` si incohérent |
| Remboursement CamPay non confirmé | `NotSupported` explicite |
| Double activation abonnement | Idempotence `IdempotencyKey` + statut terminal `Succeeded` |
| Confusion collecte vs payout MM | Namespaces / routes distincts (`api/mobile-money` vs `api/payouts/mobile-money`) |

## 7. Plan des fichiers (Lots 1–3)

### GiseBsPayGateway — créer

- `Options/MobileMoneyOptions.cs`
- `Entities/MobileMoneyWebhookEvent.cs`
- `Services/MobileMoney/IMobileMoneyGateway.cs` + modèles
- `Services/MobileMoney/CamPayMobileMoneyGateway.cs`
- `Services/MobileMoney/LocalSimulatedMobileMoneyGateway.cs`
- `Services/MobileMoney/OrangeMoneyDirectGateway.cs` (stub)
- `Services/MobileMoney/MtnMomoDirectGateway.cs` (stub)
- `Services/MobileMoney/MobileMoneyOrchestrator.cs`
- `Services/MobileMoney/MobileMoneyPhoneValidator.cs`
- `Services/MobileMoney/MobileMoneyStateMachine.cs`
- `Controllers/Api/MobileMoneyController.cs`
- `Controllers/Api/MobileMoneyWebhooksController.cs`
- Migration `AddMobileMoneyCollection`
- Tests dédiés

### GiseBsPayGateway — modifier

- `Enums/PaymentStatus.cs`, `Entities/PaymentTransaction.cs`, `Data/ApplicationDbContext.cs`
- `DTOs/ApiDtos.cs`, `Program.cs`, `Middleware/ApiKeyAuthenticationMiddleware.cs`
- `appsettings.json`, `deploy/SERVER-SECRETS.md`, `deploy/secrets.example.json`
- `Constants/CatalogOptions.cs` (ajouter `xaf`)

### TutorSphere — créer / modifier

- Docs `docs/mobile-money/*`
- DTOs + `IPaymentGatewayService` + `PayGatewayClient` / `PayGatewayService` / `PaymentsController`
- `Payment` + migration
- `PaymentCheckoutModal.razor` + clés resx FR/EN minimales

## 8. Hypothèses ouvertes (CDC §23)

- Entité juridique titulaire du compte CamPay.
- Codes marchands USSD/QR vs credentials API.
- Fonds chez TutorSphere vs école (phase 1 = compte central acté).
- Commission exacte, grâce, politique remboursement.
- Pays suivants après CM.
