-- Diagnostic du conflit « un groupe par pays » sur les groupes d'experts.
-- À exécuter en lecture seule : aucune des requêtes ci-dessous ne modifie la base.
-- Les ordres de réparation sont en fin de fichier, commentés, à n'exécuter qu'après lecture.
--
--   psql "$CONNECTION_STRING" -f docs/sql/diagnostic-groupes-experts.sql

\echo '=== 1. Index présents sur ExpertGroupsSet ==='
-- Attendu : les deux index uniques doivent contenir « IsActive = TRUE » dans leur clause WHERE.
-- S'ils s'arrêtent à « CountryCode IS NOT NULL », la base est restée sur l'ancienne règle et
-- refuse aussi les doublons inactifs : c'est la cause du 500 à l'enregistrement.
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'ExpertGroupsSet'
ORDER BY indexname;

\echo '=== 2. Migration d''assouplissement appliquée ? ==='
-- Une ligne attendue. Aucune ligne = la migration a échoué au démarrage et a été annulée.
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
WHERE "MigrationId" LIKE '%UniqueActiveExpertGroupTerritory%';

\echo '=== 3. Groupes d''experts, du plus récent au plus ancien ==='
SELECT "Id",
       "Name",
       "CountryCode",
       "IsInternational",
       "IsActive",
       "LifecycleStatus",
       "ContactEmail",
       "CreatedAt"
FROM "ExpertGroupsSet"
ORDER BY "CountryCode" NULLS FIRST, "CreatedAt";

\echo '=== 4. Territoires occupés plusieurs fois ==='
-- Toute ligne ici empêche la création de l'index unique, donc bloque la migration.
SELECT "CountryCode",
       COUNT(*)                                      AS total,
       COUNT(*) FILTER (WHERE "IsActive")            AS actifs,
       string_agg("Name" || CASE WHEN "IsActive" THEN ' (actif)' ELSE ' (inactif)' END, ' | ')
FROM "ExpertGroupsSet"
WHERE NOT "IsInternational" AND "CountryCode" IS NOT NULL
GROUP BY "CountryCode"
HAVING COUNT(*) > 1;

\echo '=== 5. Créneau international ==='
SELECT "Id", "Name", "IsActive", "LifecycleStatus"
FROM "ExpertGroupsSet"
WHERE "IsInternational";

\echo '=== 6. Rattachements du groupe, pour savoir lequel archiver sans rien perdre ==='
-- Le groupe sans membre, sans mandat ni contrat est celui qu'on archive sans conséquence.
-- Status 2 = Removed dans ExpertMembershipStatus.
SELECT g."Id",
       g."Name",
       g."CountryCode",
       g."IsActive",
       (SELECT COUNT(*) FROM "ExpertGroupMembersSet" m
         WHERE m."ExpertGroupId" = g."Id" AND m."Status" <> 2)  AS membres,
       (SELECT COUNT(*) FROM "ExpertGroupManagerMandatesSet" d
         WHERE d."ExpertGroupId" = g."Id")                      AS mandats,
       (SELECT COUNT(*) FROM "TeacherContractsSet" c
         WHERE c."ExpertGroupId" = g."Id")                      AS contrats
FROM "ExpertGroupsSet" g
ORDER BY g."CreatedAt";


-- ---------------------------------------------------------------------------
-- RÉPARATION — à exécuter seulement après avoir lu les résultats ci-dessus.
-- ---------------------------------------------------------------------------
--
-- Étape A. S'il reste deux groupes ACTIFS pour le même pays, l'index unique ne peut pas être
-- créé. Désactivez le doublon depuis l'écran d'administration (bouton Archiver), ou ici en
-- remplaçant l'identifiant. LifecycleStatus 2 = Suspended.
--
-- UPDATE "ExpertGroupsSet"
-- SET "IsActive" = FALSE, "LifecycleStatus" = 2, "UpdatedAt" = NOW()
-- WHERE "Id" = '00000000-0000-0000-0000-000000000000';
--
-- Étape B. Réaligner les index sur la règle « un seul groupe ACTIF par territoire ». Identique
-- au contenu de la migration UniqueActiveExpertGroupTerritory : à n'utiliser que si l'étape 2
-- ci-dessus ne renvoie aucune ligne et que l'étape A est faite.
--
-- BEGIN;
-- DROP INDEX IF EXISTS "IX_ExpertGroupsSet_CountryCode";
-- DROP INDEX IF EXISTS "IX_ExpertGroupsSet_IsInternational";
-- CREATE UNIQUE INDEX "IX_ExpertGroupsSet_CountryCode"
--     ON "ExpertGroupsSet" ("CountryCode")
--     WHERE "IsInternational" = FALSE AND "CountryCode" IS NOT NULL AND "IsActive" = TRUE;
-- CREATE UNIQUE INDEX "IX_ExpertGroupsSet_IsInternational"
--     ON "ExpertGroupsSet" ("IsInternational")
--     WHERE "IsInternational" = TRUE AND "IsActive" = TRUE;
-- COMMIT;
--
-- Étape C. Marquer la migration comme appliquée pour qu'EF ne la rejoue pas au démarrage.
-- N'exécuter qu'après un COMMIT réussi de l'étape B.
--
-- INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
-- VALUES ('20260820190000_UniqueActiveExpertGroupTerritory', '10.0.9')
-- ON CONFLICT DO NOTHING;
