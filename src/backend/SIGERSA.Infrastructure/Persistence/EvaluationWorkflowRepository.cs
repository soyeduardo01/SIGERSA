using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class EvaluationWorkflowRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IEvaluationWorkflowRepository
{
    private const string AccessPredicate = """
        (
            evaluation.evaluador_principal_id = @ActorId
            OR EXISTS (
                SELECT 1 FROM "SIGERSA"."ASIGNACION" assignment
                WHERE assignment.programacion_id = evaluation.programacion_id
                  AND assignment.evaluador_id = @ActorId AND assignment.estado = 'ACTIVA'
            )
            OR EXISTS (
                SELECT 1 FROM "SIGERSA"."USUARIO_ROL" user_role
                JOIN "SIGERSA"."ROL" role ON role.id = user_role.rol_id
                WHERE user_role.usuario_id = @ActorId AND user_role.activo = true
                  AND role.activo = true AND role.codigo IN ('ADMINISTRADOR', 'COORDINADOR')
                  AND user_role.vigente_desde <= CURRENT_TIMESTAMP
                  AND (user_role.vigente_hasta IS NULL OR user_role.vigente_hasta > CURRENT_TIMESTAMP)
            )
            OR EXISTS (
                SELECT 1 FROM "SIGERSA"."USUARIO" company_user
                JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
                WHERE company_user.id = @ActorId AND company_user.empresa_id = establishment.empresa_id
                  AND company_user.activo = true
            )
        )
        """;

    public async Task<EvaluationsPage> SearchAsync(EvaluationSearch query, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT evaluation.id AS Id, evaluation.numero AS Number,
                   evaluation.caso_id AS CaseId, inspection_case.numero AS CaseNumber,
                   evaluation.establecimiento_id AS EstablishmentId, establishment.nombre AS EstablishmentName,
                   evaluation.evaluador_principal_id AS EvaluatorId, evaluator.nombre_completo AS EvaluatorName,
                   evaluation.estado AS Status, evaluation.programada_inicio_en AS ScheduledStart,
                   evaluation.programada_fin_en AS ScheduledEnd,
                   evaluation.porcentaje_cumplimiento AS CompliancePercentage,
                   evaluation.riesgo_total AS TotalRisk, evaluation.nivel_riesgo AS RiskLevel,
                   (SELECT COUNT(*) FROM "SIGERSA"."RESPUESTA_USUARIO" response
                     WHERE response.evaluacion_id = evaluation.id)::integer AS AnsweredItems,
                   evaluation.version_fila AS RowVersion
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
              JOIN "SIGERSA"."USUARIO" evaluator ON evaluator.id = evaluation.evaluador_principal_id
             WHERE (@Status IS NULL OR evaluation.estado = @Status)
               AND (@Search IS NULL OR lower(evaluation.numero) LIKE '%' || @Search || '%'
                    OR lower(inspection_case.numero) LIKE '%' || @Search || '%'
                    OR lower(establishment.nombre) LIKE '%' || @Search || '%'
                    OR lower(evaluator.nombre_completo) LIKE '%' || @Search || '%')
               AND (
                    @GlobalScope = true
                    OR (@CompanyScope IS NOT NULL AND establishment.empresa_id = @CompanyScope)
                    OR (@AssignedOnly = true AND (
                        evaluation.evaluador_principal_id = @ActorId
                        OR EXISTS (
                            SELECT 1 FROM "SIGERSA"."ASIGNACION" assignment
                             WHERE assignment.programacion_id = evaluation.programacion_id
                               AND assignment.evaluador_id = @ActorId AND assignment.estado = 'ACTIVA'
                        )
                    ))
               )
             ORDER BY COALESCE(evaluation.programada_inicio_en, evaluation.creado_en) DESC, evaluation.id
             OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
              JOIN "SIGERSA"."USUARIO" evaluator ON evaluator.id = evaluation.evaluador_principal_id
             WHERE (@Status IS NULL OR evaluation.estado = @Status)
               AND (@Search IS NULL OR lower(evaluation.numero) LIKE '%' || @Search || '%'
                    OR lower(inspection_case.numero) LIKE '%' || @Search || '%'
                    OR lower(establishment.nombre) LIKE '%' || @Search || '%'
                    OR lower(evaluator.nombre_completo) LIKE '%' || @Search || '%')
               AND (
                    @GlobalScope = true
                    OR (@CompanyScope IS NOT NULL AND establishment.empresa_id = @CompanyScope)
                    OR (@AssignedOnly = true AND (
                        evaluation.evaluador_principal_id = @ActorId
                        OR EXISTS (
                            SELECT 1 FROM "SIGERSA"."ASIGNACION" assignment
                             WHERE assignment.programacion_id = evaluation.programacion_id
                               AND assignment.evaluador_id = @ActorId AND assignment.estado = 'ACTIVA'
                        )
                    ))
               );
            """;
        var parameters = new
        {
            query.Search, query.Status, query.ActorId, query.CompanyScope,
            query.GlobalScope, query.AssignedOnly,
            Offset = (query.Page - 1) * query.PageSize, query.PageSize
        };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), parameters, cancellationToken: cancellationToken));
            var items = (await result.ReadAsync<EvaluationSummaryRow>()).Select(row => row.ToDomain()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new EvaluationsPage(items, query.Page, query.PageSize, total);
        }
    }

    public async Task<EvaluationCreateOptions> GetOptionsAsync(bool canCreate, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT inspection_case.id AS Id,
                   inspection_case.numero || ' · ' || company.razon_social AS Name,
                   inspection_case.empresa_id AS CompanyId
              FROM "SIGERSA"."CASO" inspection_case
              JOIN "SIGERSA"."EMPRESA" company ON company.id = inspection_case.empresa_id
             WHERE inspection_case.estado <> 'CERRADO' AND @CanCreate = true
             ORDER BY inspection_case.abierto_en DESC;

            SELECT establishment.id AS Id, establishment.nombre AS Name, establishment.empresa_id AS CompanyId
              FROM "SIGERSA"."ESTABLECIMIENTO" establishment
             WHERE establishment.activo = true AND @CanCreate = true
             ORDER BY establishment.nombre;

            SELECT template.id AS Id, template.nombre || ' · v' || template.version::text AS Name
              FROM "SIGERSA"."FICHA_INSPECCION" template
             WHERE template.estado = 'PUBLICADA' AND @CanCreate = true
             ORDER BY template.version DESC;

            SELECT risk.id AS Id, risk.nombre || ' · v' || risk.version::text AS Name
              FROM "SIGERSA"."REGLA_RIESGO_VERSION" risk
             WHERE risk.estado = 'PUBLICADA' AND @CanCreate = true
             ORDER BY risk.version DESC;

            SELECT DISTINCT user_account.id AS Id, user_account.nombre_completo AS Name
              FROM "SIGERSA"."USUARIO" user_account
              JOIN "SIGERSA"."USUARIO_ROL" user_role ON user_role.usuario_id = user_account.id AND user_role.activo = true
              JOIN "SIGERSA"."ROL" role ON role.id = user_role.rol_id
             WHERE user_account.activo = true AND user_account.estado = 'ACTIVO'
               AND role.codigo = 'TECNICO_EVALUADOR' AND @CanCreate = true
             ORDER BY user_account.nombre_completo;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), new { CanCreate = canCreate }, cancellationToken: cancellationToken));
            var cases = (await result.ReadAsync<EvaluationOption>()).AsList();
            var establishments = (await result.ReadAsync<EvaluationOption>()).AsList();
            var templates = (await result.ReadAsync<EvaluationOption>()).AsList();
            var riskRules = (await result.ReadAsync<EvaluationOption>()).AsList();
            var evaluators = (await result.ReadAsync<EvaluationOption>()).AsList();
            return new EvaluationCreateOptions(cases, establishments, templates, riskRules, evaluators, canCreate);
        }
    }

    public async Task<PublishedInspectionTemplate> PublishAllItemsAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var source = (await connection.QueryAsync<AllItemsRow>(new CommandDefinition(Sql("""
                SELECT "Items" AS Items, "ItemsId" AS ItemsId, "Description" AS Description,
                       "SectionType" AS SectionType, "Parents" AS Parents
                FROM "SIGERSA"."AllItems" ORDER BY "Items";
                """), transaction: transaction, cancellationToken: cancellationToken))).AsList();
            if (source.Count == 0) throw new InvalidOperationException("AllItems no contiene una plantilla para publicar.");

            var previous = await connection.QuerySingleOrDefaultAsync<PreviousTemplateRow>(new CommandDefinition(Sql("""
                SELECT id AS Id, ficha_raiz_id AS RootId, version AS Version
                FROM "SIGERSA"."FICHA_INSPECCION"
                WHERE codigo = 'BPM_ALLITEMS'
                ORDER BY version DESC LIMIT 1 FOR UPDATE;
                """), transaction: transaction, cancellationToken: cancellationToken));
            var maxRiskVersion = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(Sql("""
                SELECT max(version) FROM "SIGERSA"."REGLA_RIESGO_VERSION" WHERE codigo = 'EBR_BPM_FASE1';
                """), transaction: transaction, cancellationToken: cancellationToken)) ?? 0;
            var version = Math.Max(previous?.Version ?? 0, maxRiskVersion) + 1;
            var now = DateTimeOffset.UtcNow;

            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."FICHA_INSPECCION"
                SET estado = 'RETIRADA', vigente_hasta = @Now, modificado_por = @ActorId
                WHERE codigo = 'BPM_ALLITEMS' AND estado = 'PUBLICADA';
                UPDATE "SIGERSA"."REGLA_CALIFICACION_VERSION"
                SET estado = 'RETIRADA', vigente_hasta = @Now, modificado_por = @ActorId
                WHERE codigo = 'BPM_ALLITEMS' AND estado = 'PUBLICADA';
                UPDATE "SIGERSA"."REGLA_RIESGO_VERSION"
                SET estado = 'RETIRADA', vigente_hasta = @Now, modificado_por = @ActorId
                WHERE codigo = 'EBR_BPM_FASE1' AND estado = 'PUBLICADA';
                """), new { Now = now, ActorId = actorId }, transaction, cancellationToken: cancellationToken));

            var snapshot = JsonSerializer.Serialize(new { version, items = source });
            var snapshotHash = Hash(snapshot);
            var scoreRuleId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."REGLA_CALIFICACION_VERSION"
                    (id, codigo, version, nombre, porcentaje_aprobacion, estado, definicion,
                     hash_definicion, creado_por)
                VALUES (@Id, 'BPM_ALLITEMS', @Version, 'Calificación BPM AllItems', 80,
                        'BORRADOR', @Definition::jsonb, @Hash, @ActorId);
                """), new { Id = scoreRuleId, Version = version, Definition = "{\"CUMPLE\":1,\"CUMPLE_PARCIAL\":0.5,\"NO_CUMPLE\":0,\"NO_APLICA\":null}", Hash = snapshotHash, ActorId = actorId }, transaction, cancellationToken: cancellationToken));

            var riskRuleId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."REGLA_RIESGO_VERSION"
                    (id, codigo, version, nombre, estado, formula_total, definicion,
                     peso_total, hash_definicion, creado_por)
                VALUES (@Id, 'EBR_BPM_FASE1', @Version, 'Regla EBR/BPM Fase 1', 'BORRADOR',
                        'riesgo_producto * riesgo_establecimiento', @Definition::jsonb,
                        1, @Hash, @ActorId);
                """), new { Id = riskRuleId, Version = version, Definition = "{\"establecimiento\":\"1+(1-cumplimiento)*2\"}", Hash = snapshotHash, ActorId = actorId }, transaction, cancellationToken: cancellationToken));
            var factorId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."FACTOR_RIESGO"
                    (id, regla_riesgo_version_id, codigo, nombre, peso, tipo_fuente, orden, creado_por)
                VALUES (@FactorId, @RuleId, 'BPM', 'Cumplimiento BPM', 1, 'CALCULO_BPM', 0, @ActorId);
                INSERT INTO "SIGERSA"."RANGO_RIESGO"
                    (id, regla_riesgo_version_id, codigo, nivel_riesgo, limite_inferior,
                     incluye_inferior, limite_superior, incluye_superior, frecuencia,
                     meses_frecuencia, orden, creado_por)
                VALUES
                    (gen_random_uuid(), @RuleId, 'BAJO', 'BAJO', 1, true, 3.6, true, 'ANUAL', 12, 1, @ActorId),
                    (gen_random_uuid(), @RuleId, 'MEDIO', 'MEDIO', 3.6, false, 6.3, true, 'SEMESTRAL', 6, 2, @ActorId),
                    (gen_random_uuid(), @RuleId, 'ALTO', 'ALTO', 6.3, false, NULL, false, 'TRIMESTRAL', 3, 3, @ActorId);
                """), new { FactorId = factorId, RuleId = riskRuleId, ActorId = actorId }, transaction, cancellationToken: cancellationToken));

            var templateId = Guid.NewGuid();
            var rootId = previous?.RootId ?? templateId;
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."FICHA_INSPECCION"
                    (id, ficha_raiz_id, version_anterior_id, codigo, nombre, descripcion,
                     version, estado, regla_calificacion_id, hash_definicion,
                     definicion_snapshot, creado_por)
                VALUES (@Id, @RootId, @PreviousId, 'BPM_ALLITEMS', 'Ficha BPM base',
                        'Versión inmutable publicada desde AllItems', @Version, 'BORRADOR',
                        @ScoreRuleId, @Hash, @Snapshot::jsonb, @ActorId);
                """), new { Id = templateId, RootId = rootId, PreviousId = previous?.Id, Version = version, ScoreRuleId = scoreRuleId, Hash = snapshotHash, Snapshot = snapshot, ActorId = actorId }, transaction, cancellationToken: cancellationToken));

            var idBySource = source.ToDictionary(item => item.Items, _ => Guid.NewGuid());
            var parentByCode = source
                .Where(item => !string.Equals(item.SectionType, "I", StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => item.ItemsId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Items, StringComparer.Ordinal);
            foreach (var item in source)
            {
                Guid? parentId = item.Parents is not null && parentByCode.TryGetValue(item.Parents, out var parentSource)
                    ? idBySource[parentSource]
                    : null;
                var level = GetLevel(item, source);
                var isEvaluable = string.Equals(item.SectionType, "I", StringComparison.OrdinalIgnoreCase);
                var code = $"AI-{item.Items:D4}-{item.ItemsId}";
                if (code.Length > 80) code = code[..80];
                var title = item.Description.Length > 500 ? item.Description[..500] : item.Description;
                await connection.ExecuteAsync(new CommandDefinition(Sql("""
                    INSERT INTO "SIGERSA"."ITEM_FICHA"
                        (id, ficha_inspeccion_id, parent_item_ficha_id, source_allitems_item,
                         codigo, tipo_item, titulo, descripcion, es_evaluable, tipo_respuesta,
                         nivel, orden, obligatorio, permite_no_aplica, puntaje_maximo, creado_por)
                    VALUES (@Id, @TemplateId, @ParentId, @SourceItem, @Code, @ItemType,
                            @Title, @Description, @IsEvaluable, @ResponseType, @Level,
                            @Order, @IsEvaluable, @IsEvaluable, @MaximumScore, @ActorId);
                    """), new { Id = idBySource[item.Items], TemplateId = templateId, ParentId = parentId, SourceItem = item.Items, Code = code, ItemType = isEvaluable ? "PREGUNTA" : "SECCION", Title = title, item.Description, IsEvaluable = isEvaluable, ResponseType = isEvaluable ? "OPCION_UNICA" : null, Level = level, Order = item.Items, MaximumScore = isEvaluable ? 1m : (decimal?)null, ActorId = actorId }, transaction, cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."REGLA_CALIFICACION_VERSION"
                SET estado = 'PUBLICADA', publicado_en = @Now, publicado_por = @ActorId,
                    vigente_desde = @Now, modificado_por = @ActorId
                WHERE id = @ScoreRuleId;
                UPDATE "SIGERSA"."REGLA_RIESGO_VERSION"
                SET estado = 'PUBLICADA', publicado_en = @Now, publicado_por = @ActorId,
                    vigente_desde = @Now, modificado_por = @ActorId
                WHERE id = @RiskRuleId;
                UPDATE "SIGERSA"."FICHA_INSPECCION"
                SET estado = 'PUBLICADA', publicado_en = @Now, publicado_por = @ActorId,
                    vigente_desde = @Now, modificado_por = @ActorId
                WHERE id = @TemplateId;
                """), new { Now = now, ActorId = actorId, ScoreRuleId = scoreRuleId, RiskRuleId = riskRuleId, TemplateId = templateId }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return new PublishedInspectionTemplate(templateId, riskRuleId, version, source.Count);
        }
    }

    public async Task<EvaluationSession> CreateEvaluationAsync(CreateEvaluationDraft draft, Guid actorId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "SIGERSA"."EVALUACION"
                (id, numero, caso_id, establecimiento_id, ficha_inspeccion_id,
                 evaluador_principal_id, estado, programada_inicio_en, programada_fin_en,
                 iniciada_en, version_regla_riesgo_id, creado_por)
            SELECT @Id, @Number, inspection_case.id, establishment.id, template.id,
                   evaluator.id, 'EN_EJECUCION', @ScheduledStart, @ScheduledEnd,
                   CURRENT_TIMESTAMP, risk_rule.id, @ActorId
              FROM "SIGERSA"."CASO" inspection_case
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment
                ON establishment.id = @EstablishmentId
               AND establishment.empresa_id = inspection_case.empresa_id
              JOIN "SIGERSA"."FICHA_INSPECCION" template
                ON template.id = @InspectionTemplateId AND template.estado = 'PUBLICADA'
              JOIN "SIGERSA"."REGLA_RIESGO_VERSION" risk_rule
                ON risk_rule.id = @RiskRuleVersionId AND risk_rule.estado = 'PUBLICADA'
              JOIN "SIGERSA"."USUARIO" evaluator
                ON evaluator.id = @EvaluatorId AND evaluator.activo = true AND evaluator.estado = 'ACTIVO'
             WHERE inspection_case.id = @CaseId AND inspection_case.estado <> 'CERRADO'
               AND EXISTS (
                    SELECT 1 FROM "SIGERSA"."USUARIO_ROL" user_role
                    JOIN "SIGERSA"."ROL" role ON role.id = user_role.rol_id
                    WHERE user_role.usuario_id = evaluator.id AND user_role.activo = true
                      AND role.codigo = 'TECNICO_EVALUADOR' AND role.activo = true
               )
            RETURNING id AS Id, numero AS Number, version_fila AS RowVersion;
            """;
        var id = Guid.NewGuid();
        var number = $"EV-{DateTime.UtcNow:yyyyMMdd}-{id.ToString("N")[..8].ToUpperInvariant()}";
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var created = await connection.QuerySingleOrDefaultAsync<EvaluationSession>(new CommandDefinition(
                Sql(sql), new { Id = id, Number = number, draft.CaseId, draft.EstablishmentId, draft.InspectionTemplateId, draft.EvaluatorId, draft.RiskRuleVersionId, draft.ScheduledStart, draft.ScheduledEnd, ActorId = actorId }, cancellationToken: cancellationToken));
            return created ?? throw new ArgumentException("El caso, establecimiento, ficha, regla o técnico seleccionado no está disponible.");
        }
    }

    public async Task<IReadOnlyList<EvaluationFormItem>> GetFormAsync(Guid evaluationId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT item.id AS Id, item.parent_item_ficha_id AS ParentId,
                   item.source_allitems_item AS SourceItem, item.codigo AS Code,
                   item.titulo AS Title, item.es_evaluable AS IsEvaluable,
                   item.nivel::integer AS Level, item.orden AS "Order"
            FROM "SIGERSA"."EVALUACION" evaluation
            JOIN "SIGERSA"."ITEM_FICHA" item ON item.ficha_inspeccion_id = evaluation.ficha_inspeccion_id
            WHERE evaluation.id = @EvaluationId AND {AccessPredicate}
            ORDER BY item.orden;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var rows = (await connection.QueryAsync<EvaluationFormItem>(new CommandDefinition(Sql(sql), new { EvaluationId = evaluationId, ActorId = actorId }, cancellationToken: cancellationToken))).AsList();
            if (rows.Count == 0) throw new KeyNotFoundException("La evaluación no existe o no está disponible para el usuario.");
            return rows;
        }
    }

    public async Task<EvaluationAnswer> SaveAnswerAsync(SaveEvaluationAnswerDraft draft, Guid actorId, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var contextSql = $"""
                SELECT item.id AS ItemId
                FROM "SIGERSA"."EVALUACION" evaluation
                JOIN "SIGERSA"."ITEM_FICHA" item ON item.ficha_inspeccion_id = evaluation.ficha_inspeccion_id
                WHERE evaluation.id = @EvaluationId AND item.source_allitems_item = @SourceItem
                  AND item.es_evaluable = true AND {AccessPredicate}
                FOR UPDATE;
                """;
            var itemId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(Sql(contextSql), new { draft.EvaluationId, draft.SourceItem, ActorId = actorId }, transaction, cancellationToken: cancellationToken));
            if (itemId is null) throw new KeyNotFoundException("La evaluación o el ítem evaluable no existe, o el usuario no tiene acceso.");

            var payload = JsonSerializer.Serialize(new { draft.EvaluationId, draft.SourceItem, draft.Rating, draft.Observation });
            var payloadHash = Hash(payload);
            var operationId = Guid.NewGuid();
            var inserted = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."OPERACION_SINCRONIZACION"
                    (id, usuario_id, evaluacion_id, idempotency_key, dispositivo_id,
                     secuencia_cliente, tipo_operacion, recurso_tipo, version_base,
                     payload, payload_hash, estado, fecha_cliente, creado_por)
                VALUES (@Id, @ActorId, @EvaluationId, @IdempotencyKey, @DeviceId,
                        @ClientSequence, 'GUARDAR_RESPUESTA', 'RESPUESTA_USUARIO', @BaseVersion,
                        @Payload::jsonb, @PayloadHash, 'PROCESANDO', @ClientDate, @ActorId)
                ON CONFLICT (idempotency_key) DO NOTHING;
                """), new { Id = operationId, ActorId = actorId, draft.EvaluationId, draft.IdempotencyKey, draft.DeviceId, draft.ClientSequence, draft.BaseVersion, Payload = payload, PayloadHash = payloadHash, draft.ClientDate }, transaction, cancellationToken: cancellationToken));
            if (inserted == 0)
            {
                var existing = await connection.QuerySingleAsync<SyncOperationRow>(new CommandDefinition(Sql("""
                    SELECT payload_hash AS PayloadHash, recurso_id AS ResourceId, estado AS Status
                    FROM "SIGERSA"."OPERACION_SINCRONIZACION" WHERE idempotency_key = @IdempotencyKey;
                    """), new { draft.IdempotencyKey }, transaction, cancellationToken: cancellationToken));
                if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal) || existing.ResourceId is null)
                {
                    throw new InvalidOperationException("La clave de idempotencia ya fue utilizada con otro contenido o sigue en proceso.");
                }
                var duplicate = await ReadAnswerAsync(connection, transaction, existing.ResourceId.Value, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return duplicate;
            }

            var score = draft.Rating switch { "CUMPLE" => 1m, "CUMPLE_PARCIAL" => 0.5m, "NO_CUMPLE" => 0m, _ => (decimal?)null };
            var answerId = Guid.NewGuid();
            var answer = await connection.QuerySingleOrDefaultAsync<AnswerRow>(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."RESPUESTA_USUARIO"
                    (id, evaluacion_id, item_ficha_id, valor_texto, observacion,
                     puntaje_obtenido, maximo_aplicable, respondido_por, fecha_cliente,
                     idempotency_key, creado_por)
                VALUES (@Id, @EvaluationId, @ItemId, @Rating, @Observation,
                        @Score, @MaximumScore, @ActorId, @ClientDate, @IdempotencyKey, @ActorId)
                ON CONFLICT (evaluacion_id, item_ficha_id) DO UPDATE
                SET valor_texto = EXCLUDED.valor_texto, observacion = EXCLUDED.observacion,
                    puntaje_obtenido = EXCLUDED.puntaje_obtenido,
                    maximo_aplicable = EXCLUDED.maximo_aplicable,
                    respondido_por = EXCLUDED.respondido_por,
                    respondido_en = CURRENT_TIMESTAMP, fecha_cliente = EXCLUDED.fecha_cliente,
                    idempotency_key = EXCLUDED.idempotency_key, modificado_por = @ActorId
                WHERE @BaseVersion IS NULL OR "SIGERSA"."RESPUESTA_USUARIO".version_fila = @BaseVersion
                RETURNING id AS Id, version_fila AS RowVersion;
                """), new { Id = answerId, draft.EvaluationId, ItemId = itemId.Value, draft.Rating, draft.Observation, Score = score, MaximumScore = score is null ? (decimal?)null : 1m, ActorId = actorId, draft.ClientDate, draft.IdempotencyKey, draft.BaseVersion }, transaction, cancellationToken: cancellationToken));
            if (answer is null)
            {
                var staleId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(Sql("""
                    SELECT id FROM "SIGERSA"."RESPUESTA_USUARIO"
                    WHERE evaluacion_id = @EvaluationId AND item_ficha_id = @ItemId;
                    """), new { draft.EvaluationId, ItemId = itemId.Value }, transaction, cancellationToken: cancellationToken));
                throw new OptimisticConcurrencyException(staleId);
            }
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."OPERACION_SINCRONIZACION"
                SET recurso_id = @ResourceId, estado = 'APLICADA', codigo_resultado = 'OK',
                    procesada_en = CURRENT_TIMESTAMP, modificado_por = @ActorId
                WHERE id = @OperationId;
                """), new { ResourceId = answer.Id, ActorId = actorId, OperationId = operationId }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return new EvaluationAnswer(answer.Id, draft.EvaluationId, draft.SourceItem, draft.Rating, score, answer.RowVersion);
        }
    }

    public async Task<EvaluationCalculationInput> GetCalculationInputAsync(Guid evaluationId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var evaluationSql = $"""
                SELECT evaluation.version_fila
                FROM "SIGERSA"."EVALUACION" evaluation
                WHERE evaluation.id = @EvaluationId AND {AccessPredicate};
                """;
            var rowVersion = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(Sql(evaluationSql), new { EvaluationId = evaluationId, ActorId = actorId }, cancellationToken: cancellationToken));
            if (rowVersion is null) throw new KeyNotFoundException("La evaluación no existe o no está disponible para el usuario.");
            var items = await GetFormAsync(evaluationId, actorId, cancellationToken);
            var answers = (await connection.QueryAsync<AnswerInputRow>(new CommandDefinition(Sql("""
                SELECT response.id AS Id, response.evaluacion_id AS EvaluationId,
                       item.source_allitems_item AS SourceItem, response.valor_texto AS Rating,
                       response.puntaje_obtenido AS Score, response.version_fila AS RowVersion
                FROM "SIGERSA"."RESPUESTA_USUARIO" response
                JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
                WHERE response.evaluacion_id = @EvaluationId;
                """), new { EvaluationId = evaluationId }, cancellationToken: cancellationToken))).Select(row => row.ToDomain()).ToArray();
            return new EvaluationCalculationInput(evaluationId, rowVersion.Value, items, answers);
        }
    }

    public async Task<long> SaveCalculationAsync(EvaluationCalculation calculation, string snapshotJson, string snapshotHash, long expectedVersion, Guid actorId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."EVALUACION"
            SET porcentaje_cumplimiento = @CompliancePercentage,
                riesgo_producto = @ProductRisk, riesgo_establecimiento = @EstablishmentRisk,
                riesgo_total = @TotalRisk, nivel_riesgo = @RiskLevel,
                frecuencia = @DatabaseFrequency, snapshot_calculo = @Snapshot::jsonb,
                hash_calculo = @Hash, modificado_por = @ActorId
            WHERE id = @EvaluationId AND version_fila = @ExpectedVersion
            RETURNING version_fila;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var version = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(Sql(sql), new { calculation.EvaluationId, calculation.CompliancePercentage, calculation.ProductRisk, calculation.EstablishmentRisk, calculation.TotalRisk, calculation.RiskLevel, DatabaseFrequency = calculation.Frequency == "NO_APLICA" ? null : calculation.Frequency, Snapshot = snapshotJson, Hash = snapshotHash, ActorId = actorId, ExpectedVersion = expectedVersion }, cancellationToken: cancellationToken));
            if (version is null) throw new OptimisticConcurrencyException(calculation.EvaluationId);
            return version.Value;
        }
    }

    private static async Task<EvaluationAnswer> ReadAnswerAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, Guid id, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleAsync<AnswerDetailRow>(new CommandDefinition(Sql("""
            SELECT response.id AS Id, response.evaluacion_id AS EvaluationId,
                   item.source_allitems_item AS SourceItem, response.valor_texto AS Rating,
                   response.puntaje_obtenido AS Score, response.version_fila AS RowVersion
            FROM "SIGERSA"."RESPUESTA_USUARIO" response
            JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
            WHERE response.id = @Id;
            """), new { Id = id }, transaction, cancellationToken: cancellationToken));
        return row.ToDomain();
    }

    private static int GetLevel(AllItemsRow item, IReadOnlyList<AllItemsRow> all)
    {
        var parentByCode = all.Where(value => !string.Equals(value.SectionType, "I", StringComparison.OrdinalIgnoreCase)).GroupBy(value => value.ItemsId).ToDictionary(group => group.Key, group => group.First().Parents);
        var level = 0;
        var cursor = item.Parents;
        var visited = new HashSet<string>();
        while (cursor is not null && visited.Add(cursor)) { level++; parentByCode.TryGetValue(cursor, out cursor); }
        return level;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class AllItemsRow { public int Items { get; init; } public string ItemsId { get; init; } = string.Empty; public string Description { get; init; } = string.Empty; public string SectionType { get; init; } = string.Empty; public string? Parents { get; init; } }
    private sealed class PreviousTemplateRow { public Guid Id { get; init; } public Guid RootId { get; init; } public int Version { get; init; } }
    private sealed class SyncOperationRow { public string PayloadHash { get; init; } = string.Empty; public Guid? ResourceId { get; init; } public string Status { get; init; } = string.Empty; }
    private sealed class AnswerRow { public Guid Id { get; init; } public long RowVersion { get; init; } }
    private sealed class AnswerInputRow { public Guid Id { get; init; } public Guid EvaluationId { get; init; } public int SourceItem { get; init; } public string Rating { get; init; } = string.Empty; public decimal? Score { get; init; } public long RowVersion { get; init; } public EvaluationAnswer ToDomain() => new(Id, EvaluationId, SourceItem, Rating, Score, RowVersion); }
    private sealed class AnswerDetailRow { public Guid Id { get; init; } public Guid EvaluationId { get; init; } public int SourceItem { get; init; } public string Rating { get; init; } = string.Empty; public decimal? Score { get; init; } public long RowVersion { get; init; } public EvaluationAnswer ToDomain() => new(Id, EvaluationId, SourceItem, Rating, Score, RowVersion); }
    private sealed class EvaluationSummaryRow
    {
        public Guid Id { get; init; } public string Number { get; init; } = string.Empty;
        public Guid CaseId { get; init; } public string CaseNumber { get; init; } = string.Empty;
        public Guid EstablishmentId { get; init; } public string EstablishmentName { get; init; } = string.Empty;
        public Guid EvaluatorId { get; init; } public string EvaluatorName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty; public DateTime? ScheduledStart { get; init; }
        public DateTime? ScheduledEnd { get; init; } public decimal? CompliancePercentage { get; init; }
        public decimal? TotalRisk { get; init; } public string? RiskLevel { get; init; }
        public int AnsweredItems { get; init; } public long RowVersion { get; init; }
        public EvaluationSummary ToDomain() => new(Id, Number, CaseId, CaseNumber, EstablishmentId,
            EstablishmentName, EvaluatorId, EvaluatorName, Status, Utc(ScheduledStart), Utc(ScheduledEnd),
            CompliancePercentage, TotalRisk, RiskLevel, AnsweredItems, RowVersion);
        private static DateTimeOffset? Utc(DateTime? value) => value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
    }
}
