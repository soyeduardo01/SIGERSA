BEGIN;

-- Corrige dos códigos heredados del origen SQL Server cuya descripción y sus
-- hijos ya declaraban el identificador correcto. Se usa la PK "Items" para que
-- la reparación sea determinista aun cuando "ItemsId" no sea único.
UPDATE "SIGERSA"."AllItems"
SET "ItemsId" = '1.2.2'
WHERE "Items" = 28
  AND "ItemsId" = '1.2.2.1';

UPDATE "SIGERSA"."AllItems"
SET "ItemsId" = '3.1.2'
WHERE "Items" = 59
  AND "ItemsId" = '3.1.3';

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM "SIGERSA"."AllItems" child
        WHERE child."Parents" IS NOT NULL
          AND NOT EXISTS (
              SELECT 1
              FROM "SIGERSA"."AllItems" parent
              WHERE parent."ItemsId" = child."Parents"
          )
    ) THEN
        RAISE EXCEPTION 'AllItems contiene referencias Parents sin nodo padre';
    END IF;
END;
$$;

COMMIT;
