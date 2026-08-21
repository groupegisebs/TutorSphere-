-- Diagnostic des groupes d'experts après le découplage pays / groupe.
-- La règle « un groupe par pays + un seul groupe international » n'existe plus : le pays est une
-- indication facultative et non exclusive. Seul le groupe examinateur par défaut est unique.
-- À exécuter en lecture seule : aucune des requêtes ci-dessous ne modifie la base.
-- Les ordres de réparation sont en fin de fichier, commentés, à n'exécuter qu'après lecture.
--
--   psql "$CONNECTION_STRING" -f docs/sql/diagnostic-groupes-experts.sql

\echo '=== 1. Index présents sur ExpertGroupsSet ==='
-- Attendu : IX_ExpertGroupsSet_CountryCode et IX_ExpertGroupsSet_IsInternational NON uniques,
-- et un seul index unique, IX_ExpertGroupsSet_IsDefaultReviewGroup, filtré sur les groupes actifs.
-- Un index unique restant sur CountryCode signale une base restée sur l'ancienne règle : elle
-- refusera deux groupes actifs dans le même pays, ce qui se traduit par un 500 à l'enregistrement.
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'ExpertGroupsSet'
ORDER BY indexname;

\echo '=== 2. Migration de découplage appliquée ? ==='
-- Une ligne attendue. Aucune ligne = la migration a échoué au démarrage et a été annulée.
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
WHERE "MigrationId" LIKE '%DecoupleExpertGroupFromCountry%';

\echo '=== 3. Groupes d''experts, du plus récent au plus ancien ==='
SELECT "Id",
       "Name",
       "CountryCode",
       "IsInternational",
       "IsDefaultReviewGroup",
       "IsActive",
       "LifecycleStatus",
       "ContactEmail",
       "CreatedAt"
FROM "ExpertGroupsSet"
ORDER BY "CreatedAt" DESC;

\echo '=== 4. Destinataire des candidatures spontanées ==='
-- Exactement une ligne attendue. Aucune ligne : les candidatures qu'aucun pays ne rattache
-- n'arrivent nulle part, sauf si un unique groupe est actif (repli implicite).
SELECT "Id", "Name", "CountryCode"
FROM "ExpertGroupsSet"
WHERE "IsDefaultReviewGroup" = TRUE AND "IsActive" = TRUE;

\echo '=== 5. Pays revendiqués par plusieurs groupes actifs ==='
-- Ce n'est plus une anomalie, mais le pays ne désigne alors plus de groupe examinateur :
-- les candidatures de ces pays partent vers le groupe par défaut.
SELECT "CountryCode", COUNT(*) AS groupes_actifs
FROM "ExpertGroupsSet"
WHERE "IsActive" = TRUE AND "CountryCode" IS NOT NULL
GROUP BY "CountryCode"
HAVING COUNT(*) > 1
ORDER BY groupes_actifs DESC;

-- ---------------------------------------------------------------------------
-- Réparation (à décommenter et adapter, une instruction à la fois)
-- ---------------------------------------------------------------------------

-- Désigner le groupe qui reçoit les candidatures spontanées. Le rôle est exclusif :
-- retirer l'ancien avant d'attribuer le nouveau, sinon l'index unique refuse l'écriture.
-- UPDATE "ExpertGroupsSet" SET "IsDefaultReviewGroup" = FALSE WHERE "IsDefaultReviewGroup" = TRUE;
-- UPDATE "ExpertGroupsSet" SET "IsDefaultReviewGroup" = TRUE  WHERE "Id" = '<GUID>';

-- Retirer un pays de rattachement devenu faux (le champ est facultatif).
-- UPDATE "ExpertGroupsSet" SET "CountryCode" = NULL WHERE "Id" = '<GUID>';
