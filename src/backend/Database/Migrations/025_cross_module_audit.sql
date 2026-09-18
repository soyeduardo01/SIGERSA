-- Auditoría transversal de toda entidad operativa con identificador UUID.
CREATE OR REPLACE FUNCTION "SIGERSA"."FN_AUDITAR_CAMBIO_ENTIDAD"()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = "SIGERSA", pg_catalog
AS $$
DECLARE
    fila_anterior jsonb;
    fila_nueva jsonb;
    actor_candidato uuid;
    actor_valido uuid;
    recurso uuid;
    verbo text;
BEGIN
    fila_anterior := CASE WHEN TG_OP IN ('UPDATE', 'DELETE') THEN to_jsonb(OLD) END;
    fila_nueva := CASE WHEN TG_OP IN ('INSERT', 'UPDATE') THEN to_jsonb(NEW) END;
    fila_anterior := fila_anterior - ARRAY['password_hash', 'otp_hash', 'token_hash'];
    fila_nueva := fila_nueva - ARRAY['password_hash', 'otp_hash', 'token_hash'];
    recurso := COALESCE((fila_nueva ->> 'id')::uuid, (fila_anterior ->> 'id')::uuid);
    actor_candidato := COALESCE(
        NULLIF(fila_nueva ->> 'modificado_por', '')::uuid,
        NULLIF(fila_nueva ->> 'creado_por', '')::uuid,
        NULLIF(fila_anterior ->> 'modificado_por', '')::uuid,
        NULLIF(fila_anterior ->> 'creado_por', '')::uuid);
    SELECT id INTO actor_valido
      FROM "SIGERSA"."USUARIO"
     WHERE id = actor_candidato;
    verbo := CASE TG_OP WHEN 'INSERT' THEN 'CREAR' WHEN 'UPDATE' THEN 'ACTUALIZAR' ELSE 'ELIMINAR' END;

    INSERT INTO "SIGERSA"."AUDITORIA_EVENTO"
        (id, actor_id, accion, recurso_tipo, recurso_id, resultado,
         valores_anteriores, valores_nuevos, metadatos, creado_por)
    VALUES
        (gen_random_uuid(), actor_valido, verbo || '_' || TG_TABLE_NAME, TG_TABLE_NAME,
         recurso, 'EXITOSO', fila_anterior, fila_nueva,
         jsonb_build_object('operacionSql', TG_OP, 'origen', 'TRIGGER_TRANSVERSAL'), actor_valido);

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;

DO $auditoria_transversal$
DECLARE
    tabla record;
    nombre_trigger text;
BEGIN
    FOR tabla IN
        SELECT table_name
          FROM information_schema.columns
         WHERE table_schema = 'SIGERSA'
           AND column_name = 'id'
           AND data_type = 'uuid'
           AND table_name <> 'AUDITORIA_EVENTO'
         ORDER BY table_name
    LOOP
        nombre_trigger := 'TRG_AUDITAR_' || tabla.table_name;
        EXECUTE format('DROP TRIGGER IF EXISTS %I ON "SIGERSA".%I', nombre_trigger, tabla.table_name);
        EXECUTE format(
            'CREATE TRIGGER %I AFTER INSERT OR UPDATE OR DELETE ON "SIGERSA".%I FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_AUDITAR_CAMBIO_ENTIDAD"()',
            nombre_trigger, tabla.table_name);
    END LOOP;
END;
$auditoria_transversal$;
