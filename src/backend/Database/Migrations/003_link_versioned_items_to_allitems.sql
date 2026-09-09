BEGIN;

ALTER TABLE "SIGERSA"."ITEM_FICHA"
    ADD COLUMN source_allitems_item integer;

ALTER TABLE "SIGERSA"."ITEM_FICHA"
    ADD CONSTRAINT "CK_ITEM_FICHA_SOURCE_ALLITEMS"
    CHECK (source_allitems_item IS NULL OR source_allitems_item > 0);

CREATE UNIQUE INDEX "UQ_ITEM_FICHA_SOURCE_ALLITEMS"
    ON "SIGERSA"."ITEM_FICHA" (ficha_inspeccion_id, source_allitems_item)
    WHERE source_allitems_item IS NOT NULL;

COMMENT ON COLUMN "SIGERSA"."ITEM_FICHA".source_allitems_item IS
    'Identificador de origen en AllItems. Se conserva sin FK para que la versión publicada permanezca inmutable aunque cambie la plantilla operativa.';

COMMIT;
