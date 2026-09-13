-- Applies the MakeNormalizedEmailUnique EF migration outside the application process.
-- The deployment pipeline executes this file with psql and records the result in
-- __EFMigrationsHistory, so it is safe to run repeatedly.

SELECT "NormalizedEmail", COUNT(*) AS "DuplicateCount"
FROM "AspNetUsers"
WHERE "NormalizedEmail" IS NOT NULL
GROUP BY "NormalizedEmail"
HAVING COUNT(*) > 1;

DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM "AspNetUsers"
        WHERE "NormalizedEmail" IS NOT NULL
        GROUP BY "NormalizedEmail"
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION
            'Cannot create the unique EmailIndex because duplicate NormalizedEmail values exist. Resolve duplicate accounts before deployment.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '20260910163229_MakeNormalizedEmailUnique'
    ) THEN
        DROP INDEX IF EXISTS "EmailIndex";
        CREATE UNIQUE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");

        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20260910163229_MakeNormalizedEmailUnique', '9.0.11');
    END IF;
END
$migration$;
