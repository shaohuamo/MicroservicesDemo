using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductsMicroservice.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialProductsSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // IF NOT EXISTS lets installations created by the former bootstrap DDL
        // adopt EF migrations without dropping existing product or outbox data.
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS public."Products"
            (
                "ProductId" uuid NOT NULL,
                "ProductName" character varying(50) NOT NULL,
                "DisplayName" character varying(50) NOT NULL,
                "QuantityInStock" integer NULL,
                "UnitPrice" numeric(10,2) NULL,
                "Version" integer NOT NULL DEFAULT 1,
                CONSTRAINT "PK_Products" PRIMARY KEY ("ProductId")
            );

            CREATE TABLE IF NOT EXISTS public."ProductOperationOutbox"
            (
                "NotificationId" uuid NOT NULL,
                "OccurredAtUtc" timestamp with time zone NOT NULL,
                "Payload" jsonb NOT NULL,
                "PayloadHash" character varying(64) NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "PublishedAtUtc" timestamp with time zone NULL,
                "AttemptCount" integer NOT NULL DEFAULT 0,
                "NextAttemptAtUtc" timestamp with time zone NULL,
                "LastError" character varying(512) NULL,
                "LockedBy" character varying(200) NULL,
                "LockedUntilUtc" timestamp with time zone NULL,
                "Version" bigint NOT NULL DEFAULT 0,
                CONSTRAINT "PK_ProductOperationOutbox" PRIMARY KEY ("NotificationId")
            );

            DO $migration$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conrelid = 'public."Products"'::regclass
                      AND conname = 'Products_pkey')
                   AND NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conrelid = 'public."Products"'::regclass
                      AND conname = 'PK_Products') THEN
                    ALTER TABLE public."Products"
                        RENAME CONSTRAINT "Products_pkey" TO "PK_Products";
                END IF;

                IF EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conrelid = 'public."ProductOperationOutbox"'::regclass
                      AND conname = 'ProductOperationOutbox_pkey')
                   AND NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conrelid = 'public."ProductOperationOutbox"'::regclass
                      AND conname = 'PK_ProductOperationOutbox') THEN
                    ALTER TABLE public."ProductOperationOutbox"
                        RENAME CONSTRAINT "ProductOperationOutbox_pkey" TO "PK_ProductOperationOutbox";
                END IF;
            END $migration$;

            ALTER TABLE public."Products"
                ADD COLUMN IF NOT EXISTS "DisplayName" character varying(50);

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM public."Products"
                    GROUP BY upper(regexp_replace(normalize("ProductName", NFKC), '[[:space:]]+', '', 'g'))
                    HAVING count(*) > 1
                ) THEN
                    RAISE EXCEPTION 'Cannot migrate Products: normalized ProductName values are duplicated. Merge or rename the conflicting products before retrying.';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM public."Products"
                    WHERE length(upper(regexp_replace(normalize("ProductName", NFKC), '[[:space:]]+', '', 'g'))) > 50
                ) THEN
                    RAISE EXCEPTION 'Cannot migrate Products: a normalized ProductName exceeds 50 characters.';
                END IF;
            END $$;

            UPDATE public."Products"
            SET "DisplayName" = "ProductName",
                "ProductName" = upper(regexp_replace(normalize("ProductName", NFKC), '[[:space:]]+', '', 'g'))
            WHERE "DisplayName" IS NULL;

            ALTER TABLE public."Products"
                ALTER COLUMN "DisplayName" SET NOT NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Products_ProductName"
                ON public."Products" ("ProductName");

            CREATE INDEX IF NOT EXISTS "IX_ProductOperationOutbox_Dispatch"
                ON public."ProductOperationOutbox"
                ("PublishedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc", "CreatedAtUtc");

            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProductOperationOutbox",
            schema: "public");

        migrationBuilder.DropTable(
            name: "Products",
            schema: "public");
    }
}
