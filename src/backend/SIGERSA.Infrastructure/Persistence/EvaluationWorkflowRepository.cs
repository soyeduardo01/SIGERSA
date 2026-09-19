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
                   evaluation.frecuencia AS Frequency,
                   inspection_case.proxima_inspeccion_en AS NextInspectionAt,
                   (SELECT COUNT(*) FROM "SIGERSA"."RESPUESTA_USUARIO" response
                     WHERE response.evaluacion_id = evaluation.id)::integer AS AnsweredItems,
                   EXISTS (
                       SELECT 1
                         FROM "SIGERSA"."INFORME" report
                         JOIN "SIGERSA"."INFORME_VERSION" version ON version.informe_id = report.id
                        WHERE report.evaluacion_id = evaluation.id AND version.es_oficial = true
                   ) AS HasOfficialReport,
                   (@CanExecute = true AND evaluation.estado = 'EN_EJECUCION' AND (
                       evaluation.evaluador_principal_id = @ActorId
                       OR EXISTS (
                           SELECT 1 FROM "SIGERSA"."ASIGNACION" editable_assignment
                            WHERE editable_assignment.programacion_id = evaluation.programacion_id
                              AND editable_assignment.evaluador_id = @ActorId
                              AND editable_assignment.estado = 'ACTIVA'
                       )
                   )) AS CanEdit,
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
                    OR (@CompanyScope IS NOT NULL AND establishment.empresa_id = @CompanyScope AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."SOLICITUD" owner_request
                         WHERE owner_request.id = inspection_case.solicitud_id AND owner_request.solicitante_id = @ActorId))
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
                    OR (@CompanyScope IS NOT NULL AND establishment.empresa_id = @CompanyScope AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."SOLICITUD" owner_request
                         WHERE owner_request.id = inspection_case.solicitud_id AND owner_request.solicitante_id = @ActorId))
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
            query.Search,
            query.Status,
            query.ActorId,
            query.CompanyScope,
            query.GlobalScope,
            query.AssignedOnly,
            query.CanExecute,
            Offset = (query.Page - 1) * query.PageSize,
            query.PageSize
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

            SELECT template.id AS Id, template.nombre || ' · v' || template.version::text AS Name,
                   NULL::uuid AS CompanyId
              FROM "SIGERSA"."FICHA_INSPECCION" template
             WHERE template.estado = 'PUBLICADA' AND @CanCreate = true
             ORDER BY template.version DESC;

            SELECT risk.id AS Id, risk.nombre || ' · v' || risk.version::text AS Name,
                   NULL::uuid AS CompanyId
              FROM "SIGERSA"."REGLA_RIESGO_VERSION" risk
             WHERE risk.estado = 'PUBLICADA' AND @CanCreate = true
             ORDER BY risk.version DESC;

            SELECT DISTINCT user_account.id AS Id, user_account.nombre_completo AS Name,
                   user_account.empresa_id AS CompanyId
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
                """), new { Id = riskRuleId, Version = version, Definition = "{\"establecimiento\":\"SUM(puntaje_factor*peso)\",\"total\":\"riesgo_producto*riesgo_establecimiento\"}", Hash = snapshotHash, ActorId = actorId }, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."FACTOR_RIESGO"
                    (id, regla_riesgo_version_id, codigo, nombre, peso, tipo_fuente, orden, creado_por)
                VALUES
                    (gen_random_uuid(), @RuleId, 'VOLUMEN_PRODUCCION', 'Volumen de producción', 0.16, 'ESTABLECIMIENTO', 1, @ActorId),
                    (gen_random_uuid(), @RuleId, 'HACCP', 'Implementación sistema HACCP', 0.09, 'ESTABLECIMIENTO', 2, @ActorId),
                    (gen_random_uuid(), @RuleId, 'BPM', 'Cumplimiento con las BPM', 0.56, 'CALCULO_BPM', 3, @ActorId),
                    (gen_random_uuid(), @RuleId, 'INABIE', 'Proveedor INABIE y alcance de distribución', 0.05, 'ESTABLECIMIENTO', 4, @ActorId),
                    (gen_random_uuid(), @RuleId, 'RECHAZOS', 'Rechazos microbiológicos en registros sanitarios', 0.06, 'ESTABLECIMIENTO', 5, @ActorId),
                    (gen_random_uuid(), @RuleId, 'MUESTREO', 'Plan de muestreo microbiológico y análisis de laboratorio', 0.08, 'ESTABLECIMIENTO', 6, @ActorId);

                INSERT INTO "SIGERSA"."FACTOR_RIESGO_OPCION"
                    (id, factor_riesgo_id, codigo, nombre, puntaje, orden, creado_por)
                SELECT gen_random_uuid(), factor.id, option.codigo, option.nombre, option.puntaje, option.orden, @ActorId
                FROM "SIGERSA"."FACTOR_RIESGO" factor
                JOIN (VALUES
                    ('VOLUMEN_PRODUCCION', 'MICRO', 'Micro (<200.000 por mes)', 1.00, 1),
                    ('VOLUMEN_PRODUCCION', 'PEQUENO', 'Pequeño (200.000 - 800.000 por mes)', 1.67, 2),
                    ('VOLUMEN_PRODUCCION', 'MEDIANO', 'Mediano (800.000 - 2.000.000 por mes)', 2.33, 3),
                    ('VOLUMEN_PRODUCCION', 'GRANDE', 'Grande (>2.000.000 por mes)', 3.00, 4),
                    ('HACCP', 'TODAS_LINEAS', 'HACCP en todas las líneas de producción', 1.00, 1),
                    ('HACCP', 'SETENTA_Y_CINCO', 'HACCP en el 75% de las líneas de producción', 1.67, 2),
                    ('HACCP', 'VEINTICINCO', 'HACCP en el 25% de las líneas de producción', 2.33, 3),
                    ('HACCP', 'NO_IMPLEMENTADO', 'No tiene implementado el sistema HACCP', 3.00, 4),
                    ('BPM', 'MAYOR_95', 'Cumplimiento BPM mayor de 95%', 1.00, 1),
                    ('BPM', 'ENTRE_90_95', 'Cumplimiento BPM entre 90% y 95%', 1.67, 2),
                    ('BPM', 'ENTRE_82_89', 'Cumplimiento BPM entre 82% y 89%', 2.33, 3),
                    ('BPM', 'HASTA_81', 'Cumplimiento BPM menor o igual a 81%', 3.00, 4),
                    ('INABIE', 'NO_SUPLIDOR', 'No es suplidor del INABIE', 1.00, 1),
                    ('INABIE', 'LOCAL', 'Distribución local', 1.67, 2),
                    ('INABIE', 'REGIONAL', 'Distribución regional', 2.33, 3),
                    ('INABIE', 'NACIONAL', 'Distribución nacional', 3.00, 4),
                    ('RECHAZOS', 'NINGUNO', 'Ningún rechazo en los últimos 5 años', 1.00, 1),
                    ('RECHAZOS', 'UNO', 'Un rechazo en los últimos 5 años', 1.67, 2),
                    ('RECHAZOS', 'DOS', 'Dos rechazos en los últimos 5 años', 2.33, 3),
                    ('RECHAZOS', 'MAS_DE_DOS', 'Más de dos rechazos en los últimos 5 años', 3.00, 4),
                    ('MUESTREO', 'COMPLETO', 'Materias primas, áreas de proceso y productos terminados', 1.00, 1),
                    ('MUESTREO', 'PROCESO_TERMINADOS', 'Áreas de proceso y productos terminados', 1.67, 2),
                    ('MUESTREO', 'MATERIAS_PRIMAS', 'Solo materias primas', 2.33, 3),
                    ('MUESTREO', 'SIN_PLAN', 'No cuenta con plan de muestreo microbiológico', 3.00, 4)
                ) AS option(factor_codigo, codigo, nombre, puntaje, orden)
                  ON option.factor_codigo = factor.codigo
                WHERE factor.regla_riesgo_version_id = @RuleId;

                INSERT INTO "SIGERSA"."RANGO_RIESGO"
                    (id, regla_riesgo_version_id, codigo, nivel_riesgo, limite_inferior,
                     incluye_inferior, limite_superior, incluye_superior, frecuencia,
                     meses_frecuencia, orden, creado_por)
                VALUES
                    (gen_random_uuid(), @RuleId, 'BAJO', 'BAJO', 1, true, 3.6, true, 'ANUAL', 12, 1, @ActorId),
                    (gen_random_uuid(), @RuleId, 'MEDIO', 'MEDIO', 3.6, false, 6.3, true, 'SEMESTRAL', 6, 2, @ActorId),
                    (gen_random_uuid(), @RuleId, 'ALTO', 'ALTO', 6.3, false, NULL, false, 'TRIMESTRAL', 3, 3, @ActorId);
                """), new { RuleId = riskRuleId, ActorId = actorId }, transaction, cancellationToken: cancellationToken));

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
                   evaluator.id, 'ASIGNADA', @ScheduledStart, @ScheduledEnd,
                   NULL, risk_rule.id, @ActorId
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

    public async Task<EvaluationWorkspaceData> GetWorkspaceAsync(
        Guid evaluationId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var items = await GetFormAsync(evaluationId, actorId, cancellationToken);
        const string sql = """
            SELECT item.source_allitems_item AS SourceItem,
                   response.valor_texto AS Rating,
                   criticality.codigo AS CriticalityCode,
                   response.observacion AS Observation,
                   response.comentario AS Comment
              FROM "SIGERSA"."RESPUESTA_USUARIO" response
              JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
              LEFT JOIN "SIGERSA"."NIVEL_CRITICIDAD" criticality
                ON criticality.id = response.nivel_criticidad_id
             WHERE response.evaluacion_id = @EvaluationId
             ORDER BY item.orden;

            SELECT evidence.id AS Id, item.source_allitems_item AS SourceItem,
                   evidence.nombre_original AS OriginalName,
                   evidence.fecha_servidor AS UploadedAt
              FROM "SIGERSA"."EVIDENCIA" evidence
              JOIN "SIGERSA"."EVIDENCIA_RESPUESTA" evidence_response
                ON evidence_response.evidencia_id = evidence.id
              JOIN "SIGERSA"."RESPUESTA_USUARIO" response
                ON response.id = evidence_response.respuesta_usuario_id
              JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
             WHERE evidence.evaluacion_id = @EvaluationId
               AND item.source_allitems_item IS NOT NULL
               AND evidence.eliminada = false
             ORDER BY item.source_allitems_item, evidence.fecha_servidor, evidence.id;

            SELECT fecha_ultima_inspeccion AS PreviousInspectionDate,
                   calificacion_ultima_inspeccion AS PreviousQualification,
                   fecha_inspeccion_actual AS CurrentInspectionDate,
                   calificacion_inspeccion_actual AS CurrentQualification,
                   oficial_dps_das_1 AS DpsDasOfficer1,
                   oficial_dps_das_2 AS DpsDasOfficer2,
                   tecnico_digemaps_1 AS DigemapsTechnician1,
                   tecnico_digemaps_2 AS DigemapsTechnician2,
                   medidas_correctivas::text AS CorrectiveMeasuresJson,
                   recomendaciones::text AS RecommendationsJson,
                   version_fila AS RowVersion
              FROM "SIGERSA"."EVALUACION_COMPLEMENTO"
             WHERE evaluacion_id = @EvaluationId;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), new { EvaluationId = evaluationId }, cancellationToken: cancellationToken));
            var answers = (await result.ReadAsync<EvaluationSavedAnswer>()).AsList();
            var evidences = (await result.ReadAsync<EvaluationSavedEvidenceRow>())
                .Select(row => row.ToDomain()).ToArray();
            var supplement = await result.ReadSingleOrDefaultAsync<EvaluationSupplementRow>();
            return new EvaluationWorkspaceData(
                items, answers, evidences, supplement?.ToDomain() ?? EmptySupplement());
        }
    }

    public async Task<EvaluationInspectionContext> GetInspectionContextAsync(
        Guid evaluationId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var contextSql = $"""
            SELECT inspection_case.origen AS Origin,
                   reason.codigo AS InspectionReasonCode,
                   reason.nombre AS InspectionReasonName,
                   (NULLIF(trim(establishment.permiso_sanitario_numero), '') IS NOT NULL
                    AND (establishment.permiso_sanitario_vence_en IS NULL
                         OR establishment.permiso_sanitario_vence_en::date >= CURRENT_DATE)) AS HasValidSanitaryPermit,
                   (SELECT prior.id
                      FROM "SIGERSA"."EVALUACION" prior
                     WHERE prior.establecimiento_id = evaluation.establecimiento_id
                       AND prior.id <> evaluation.id
                       AND prior.estado IN ('FINALIZADA', 'ENVIADA', 'EN_REVISION', 'APROBADA', 'CERRADA')
                       AND COALESCE(prior.finalizada_en, prior.enviada_en, prior.aprobada_en,
                                    prior.iniciada_en, prior.creado_en)
                           <= COALESCE(evaluation.iniciada_en, evaluation.creado_en)
                     ORDER BY COALESCE(prior.finalizada_en, prior.iniciada_en, prior.creado_en) DESC, prior.id DESC
                     LIMIT 1) AS PreviousEvaluationId,
                   (SELECT COALESCE(prior.finalizada_en, prior.iniciada_en)::date
                      FROM "SIGERSA"."EVALUACION" prior
                     WHERE prior.establecimiento_id = evaluation.establecimiento_id
                       AND prior.id <> evaluation.id
                       AND prior.estado IN ('FINALIZADA', 'ENVIADA', 'EN_REVISION', 'APROBADA', 'CERRADA')
                       AND COALESCE(prior.finalizada_en, prior.enviada_en, prior.aprobada_en,
                                    prior.iniciada_en, prior.creado_en)
                           <= COALESCE(evaluation.iniciada_en, evaluation.creado_en)
                     ORDER BY COALESCE(prior.finalizada_en, prior.iniciada_en, prior.creado_en) DESC, prior.id DESC
                     LIMIT 1) AS PreviousInspectionDate,
                   (SELECT prior.porcentaje_cumplimiento
                      FROM "SIGERSA"."EVALUACION" prior
                     WHERE prior.establecimiento_id = evaluation.establecimiento_id
                       AND prior.id <> evaluation.id
                       AND prior.estado IN ('FINALIZADA', 'ENVIADA', 'EN_REVISION', 'APROBADA', 'CERRADA')
                       AND COALESCE(prior.finalizada_en, prior.enviada_en, prior.aprobada_en,
                                    prior.iniciada_en, prior.creado_en)
                           <= COALESCE(evaluation.iniciada_en, evaluation.creado_en)
                     ORDER BY COALESCE(prior.finalizada_en, prior.iniciada_en, prior.creado_en) DESC, prior.id DESC
                     LIMIT 1) AS PreviousCompliancePercentage
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
              LEFT JOIN "SIGERSA"."MOTIVO_INSPECCION" reason ON reason.id = inspection_case.motivo_inspeccion_id
             WHERE evaluation.id = @EvaluationId AND {AccessPredicate};
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var header = await connection.QuerySingleOrDefaultAsync<InspectionContextRow>(new CommandDefinition(
                Sql(contextSql), new { EvaluationId = evaluationId, ActorId = actorId }, cancellationToken: cancellationToken));
            if (header is null)
                throw new KeyNotFoundException("La evaluación no existe o no está disponible para el usuario.");

            var priorNonconformities = header.PreviousEvaluationId.HasValue
                ? (await connection.QueryAsync<int>(new CommandDefinition(Sql("""
                    SELECT DISTINCT item.source_allitems_item
                      FROM "SIGERSA"."RESPUESTA_USUARIO" response
                      JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
                     WHERE response.evaluacion_id = @PreviousEvaluationId
                       AND response.valor_texto = 'NO_CUMPLE'
                       AND item.source_allitems_item IS NOT NULL
                     ORDER BY item.source_allitems_item;
                    """), new { header.PreviousEvaluationId }, cancellationToken: cancellationToken))).ToArray()
                : [];

            return new EvaluationInspectionContext(
                header.Origin,
                header.InspectionReasonCode,
                header.InspectionReasonName,
                header.HasValidSanitaryPermit,
                header.PreviousEvaluationId,
                header.PreviousInspectionDate,
                header.PreviousCompliancePercentage,
                priorNonconformities);
        }
    }

    public async Task<EvaluationSupplement> SaveSupplementAsync(
        Guid evaluationId,
        SaveEvaluationSupplementDraft draft,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var accessSql = $"""
                SELECT evaluation.id
                  FROM "SIGERSA"."EVALUACION" evaluation
                 WHERE evaluation.id = @EvaluationId
                   AND evaluation.estado = 'EN_EJECUCION'
                   AND {AccessPredicate}
                 FOR UPDATE;
                """;
            var accessibleId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                Sql(accessSql), new { EvaluationId = evaluationId, ActorId = actorId }, transaction,
                cancellationToken: cancellationToken));
            if (accessibleId is null)
                throw new KeyNotFoundException("La evaluación no está en ejecución o no está disponible para el usuario.");

            var saved = await connection.QuerySingleOrDefaultAsync<EvaluationSupplementRow>(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."EVALUACION_COMPLEMENTO" AS complement
                    (evaluacion_id, fecha_ultima_inspeccion, calificacion_ultima_inspeccion,
                     fecha_inspeccion_actual, calificacion_inspeccion_actual,
                     oficial_dps_das_1, oficial_dps_das_2,
                     tecnico_digemaps_1, tecnico_digemaps_2,
                     medidas_correctivas, recomendaciones, creado_por, modificado_por)
                VALUES
                    (@EvaluationId, @PreviousInspectionDate, @PreviousQualification,
                     @CurrentInspectionDate, @CurrentQualification,
                     @DpsDasOfficer1, @DpsDasOfficer2,
                     @DigemapsTechnician1, @DigemapsTechnician2,
                     @CorrectiveMeasures::jsonb, @Recommendations::jsonb, @ActorId, @ActorId)
                ON CONFLICT (evaluacion_id) DO UPDATE
                   SET fecha_ultima_inspeccion = EXCLUDED.fecha_ultima_inspeccion,
                       calificacion_ultima_inspeccion = EXCLUDED.calificacion_ultima_inspeccion,
                       fecha_inspeccion_actual = EXCLUDED.fecha_inspeccion_actual,
                       calificacion_inspeccion_actual = EXCLUDED.calificacion_inspeccion_actual,
                       oficial_dps_das_1 = EXCLUDED.oficial_dps_das_1,
                       oficial_dps_das_2 = EXCLUDED.oficial_dps_das_2,
                       tecnico_digemaps_1 = EXCLUDED.tecnico_digemaps_1,
                       tecnico_digemaps_2 = EXCLUDED.tecnico_digemaps_2,
                       medidas_correctivas = EXCLUDED.medidas_correctivas,
                       recomendaciones = EXCLUDED.recomendaciones,
                       modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId,
                       version_fila = complement.version_fila + 1
                 WHERE @RowVersion > 0 AND complement.version_fila = @RowVersion
                RETURNING fecha_ultima_inspeccion AS PreviousInspectionDate,
                          calificacion_ultima_inspeccion AS PreviousQualification,
                          fecha_inspeccion_actual AS CurrentInspectionDate,
                          calificacion_inspeccion_actual AS CurrentQualification,
                          oficial_dps_das_1 AS DpsDasOfficer1,
                          oficial_dps_das_2 AS DpsDasOfficer2,
                          tecnico_digemaps_1 AS DigemapsTechnician1,
                          tecnico_digemaps_2 AS DigemapsTechnician2,
                          medidas_correctivas::text AS CorrectiveMeasuresJson,
                          recomendaciones::text AS RecommendationsJson,
                          version_fila AS RowVersion;
                """), new
            {
                EvaluationId = evaluationId,
                PreviousInspectionDate = draft.PreviousInspectionDate?.ToDateTime(TimeOnly.MinValue),
                draft.PreviousQualification,
                CurrentInspectionDate = draft.CurrentInspectionDate?.ToDateTime(TimeOnly.MinValue),
                draft.CurrentQualification,
                draft.DpsDasOfficer1,
                draft.DpsDasOfficer2,
                draft.DigemapsTechnician1,
                draft.DigemapsTechnician2,
                CorrectiveMeasures = JsonSerializer.Serialize(draft.CorrectiveMeasures),
                Recommendations = JsonSerializer.Serialize(draft.Recommendations),
                RowVersion = draft.RowVersion,
                ActorId = actorId
            }, transaction, cancellationToken: cancellationToken));
            if (saved is null) throw new OptimisticConcurrencyException(evaluationId);
            await transaction.CommitAsync(cancellationToken);
            return saved.ToDomain();
        }
    }

    public async Task<EvaluationAnswer> SaveAnswerAsync(SaveEvaluationAnswerDraft draft, Guid actorId, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var evaluationSql = $"""
                SELECT evaluation.estado
                  FROM "SIGERSA"."EVALUACION" evaluation
                 WHERE evaluation.id = @EvaluationId AND {AccessPredicate}
                 FOR UPDATE;
                """;
            var evaluationStatus = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                Sql(evaluationSql), new { draft.EvaluationId, ActorId = actorId }, transaction,
                cancellationToken: cancellationToken));
            if (evaluationStatus is null)
                throw new KeyNotFoundException("La evaluación no existe o el usuario no tiene acceso.");
            if (evaluationStatus is not "EN_EJECUCION")
                throw new InvalidOperationException(
                    "La ficha solo admite respuestas cuando la evaluación está en ejecución.");

            var itemId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(Sql("""
                SELECT item.id
                  FROM "SIGERSA"."EVALUACION" evaluation
                  JOIN "SIGERSA"."ITEM_FICHA" item
                    ON item.ficha_inspeccion_id = evaluation.ficha_inspeccion_id
                 WHERE evaluation.id = @EvaluationId
                   AND item.source_allitems_item = @SourceItem
                   AND item.es_evaluable = true AND item.activo = true;
                """), new { draft.EvaluationId, draft.SourceItem }, transaction,
                cancellationToken: cancellationToken));
            if (itemId is null)
                throw new ArgumentException("El ítem no pertenece a la ficha evaluable seleccionada.");

            var payload = JsonSerializer.Serialize(new
            {
                draft.EvaluationId,
                draft.SourceItem,
                draft.Rating,
                draft.CriticalityCode,
                draft.Observation,
                draft.Comment
            });
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
                    (id, evaluacion_id, item_ficha_id, valor_texto, nivel_criticidad_id,
                     observacion, comentario,
                     puntaje_obtenido, maximo_aplicable, respondido_por, fecha_cliente,
                     idempotency_key, creado_por)
                VALUES (@Id, @EvaluationId, @ItemId, @Rating,
                        (SELECT id FROM "SIGERSA"."NIVEL_CRITICIDAD"
                          WHERE codigo = @CriticalityCode AND activo = true LIMIT 1),
                        @Observation, @Comment,
                        @Score, @MaximumScore, @ActorId, @ClientDate, @IdempotencyKey, @ActorId)
                ON CONFLICT (evaluacion_id, item_ficha_id) DO UPDATE
                SET valor_texto = EXCLUDED.valor_texto, observacion = EXCLUDED.observacion,
                    comentario = EXCLUDED.comentario,
                    nivel_criticidad_id = EXCLUDED.nivel_criticidad_id,
                    puntaje_obtenido = EXCLUDED.puntaje_obtenido,
                    maximo_aplicable = EXCLUDED.maximo_aplicable,
                    respondido_por = EXCLUDED.respondido_por,
                    respondido_en = CURRENT_TIMESTAMP, fecha_cliente = EXCLUDED.fecha_cliente,
                    idempotency_key = EXCLUDED.idempotency_key, modificado_por = @ActorId
                WHERE @BaseVersion IS NULL OR "SIGERSA"."RESPUESTA_USUARIO".version_fila = @BaseVersion
                RETURNING id AS Id, version_fila AS RowVersion;
                """), new { Id = answerId, draft.EvaluationId, ItemId = itemId.Value, draft.Rating, draft.CriticalityCode, draft.Observation, draft.Comment, Score = score, MaximumScore = score is null ? (decimal?)null : 1m, ActorId = actorId, draft.ClientDate, draft.IdempotencyKey, draft.BaseVersion }, transaction, cancellationToken: cancellationToken));
            if (answer is null)
            {
                var staleId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(Sql("""
                    SELECT id FROM "SIGERSA"."RESPUESTA_USUARIO"
                    WHERE evaluacion_id = @EvaluationId AND item_ficha_id = @ItemId;
                    """), new { draft.EvaluationId, ItemId = itemId.Value }, transaction, cancellationToken: cancellationToken));
                throw new OptimisticConcurrencyException(staleId);
            }
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."NO_CONFORMIDAD"
                    (id, evaluacion_id, item_ficha_id, respuesta_usuario_id,
                     nivel_criticidad_id, codigo, descripcion, estado,
                     detectada_en, creado_por, modificado_por)
                SELECT gen_random_uuid(), response.evaluacion_id, response.item_ficha_id,
                       response.id, response.nivel_criticidad_id,
                       'NC-' || upper(substr(replace(response.id::text, '-', ''), 1, 10)),
                       COALESCE(NULLIF(response.observacion, ''), NULLIF(response.comentario, ''), item.titulo),
                       'ABIERTA', CURRENT_TIMESTAMP, @ActorId, @ActorId
                  FROM "SIGERSA"."RESPUESTA_USUARIO" response
                  JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
                 WHERE response.id = @ResponseId
                   AND @Rating = 'NO_CUMPLE'
                   AND response.nivel_criticidad_id IS NOT NULL
                ON CONFLICT (evaluacion_id, item_ficha_id) DO UPDATE
                    SET respuesta_usuario_id = EXCLUDED.respuesta_usuario_id,
                        nivel_criticidad_id = EXCLUDED.nivel_criticidad_id,
                        descripcion = EXCLUDED.descripcion,
                        estado = 'ABIERTA', cerrada_en = NULL, cierre_justificacion = NULL,
                        modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                        version_fila = "SIGERSA"."NO_CONFORMIDAD".version_fila + 1;

                UPDATE "SIGERSA"."NO_CONFORMIDAD"
                   SET estado = 'CERRADO', cerrada_en = CURRENT_TIMESTAMP,
                       cierre_justificacion = 'Cerrada automáticamente al cambiar la respuesta a cumplimiento.',
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE evaluacion_id = @EvaluationId AND item_ficha_id = @ItemId
                   AND @Rating <> 'NO_CUMPLE' AND estado <> 'CERRADO';

                WITH RECURSIVE inspection_lineage AS (
                    SELECT (inspection_case.metadatos_origen ->> 'evaluacionAnteriorId')::uuid AS evaluation_id
                      FROM "SIGERSA"."EVALUACION" evaluation
                      JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
                     WHERE evaluation.id = @EvaluationId
                       AND inspection_case.metadatos_origen ->> 'tipo' = 'REINSPECCION_NO_CONFORMIDADES'
                       AND inspection_case.metadatos_origen ->> 'evaluacionAnteriorId' IS NOT NULL
                    UNION ALL
                    SELECT (ancestor_case.metadatos_origen ->> 'evaluacionAnteriorId')::uuid
                      FROM inspection_lineage lineage
                      JOIN "SIGERSA"."EVALUACION" ancestor ON ancestor.id = lineage.evaluation_id
                      JOIN "SIGERSA"."CASO" ancestor_case ON ancestor_case.id = ancestor.caso_id
                     WHERE ancestor_case.metadatos_origen ->> 'tipo' = 'REINSPECCION_NO_CONFORMIDADES'
                       AND ancestor_case.metadatos_origen ->> 'evaluacionAnteriorId' IS NOT NULL
                )
                UPDATE "SIGERSA"."NO_CONFORMIDAD" nonconformity
                   SET estado = CASE WHEN @Rating = 'NO_CUMPLE' THEN 'EN_CORRECCION' ELSE 'VALIDADO' END,
                       cerrada_en = NULL,
                       cierre_justificacion = NULL,
                       modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId,
                       version_fila = nonconformity.version_fila + 1
                  FROM "SIGERSA"."ITEM_FICHA" finding_item
                 WHERE nonconformity.evaluacion_id IN (SELECT evaluation_id FROM inspection_lineage)
                   AND finding_item.id = nonconformity.item_ficha_id
                   AND finding_item.source_allitems_item = @SourceItem
                   AND nonconformity.estado <> 'CERRADO'
                   AND nonconformity.estado IS DISTINCT FROM
                       CASE WHEN @Rating = 'NO_CUMPLE' THEN 'EN_CORRECCION' ELSE 'VALIDADO' END;
                """), new
            {
                ResponseId = answer.Id,
                draft.EvaluationId,
                ItemId = itemId.Value,
                draft.SourceItem,
                draft.Rating,
                ActorId = actorId
            }, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."OPERACION_SINCRONIZACION"
                SET recurso_id = @ResourceId, estado = 'APLICADA', codigo_resultado = 'OK',
                    procesada_en = CURRENT_TIMESTAMP, modificado_por = @ActorId
                WHERE id = @OperationId;
                """), new { ResourceId = answer.Id, ActorId = actorId, OperationId = operationId }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return new EvaluationAnswer(answer.Id, draft.EvaluationId, draft.SourceItem, draft.Rating, draft.CriticalityCode, score, answer.RowVersion);
        }
    }

    public async Task<EvaluationCalculationInput> GetCalculationInputAsync(Guid evaluationId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var evaluationSql = $"""
                SELECT evaluation.version_fila AS RowVersion,
                       (SELECT MAX(subcategory.nivel_riesgo)::numeric
                          FROM "SIGERSA"."ESTABLECIMIENTO_PRODUCTO" product
                          JOIN "SIGERSA"."SUBCATEGORIA_ALIMENTO" subcategory
                            ON subcategory.id = product.subcategoria_alimento_id
                         WHERE product.establecimiento_id = establishment.id
                           AND product.activo = true AND subcategory.activo = true) AS ProductRisk,
                       COALESCE((SELECT SUM(product.volumen_mensual)
                                 FROM "SIGERSA"."ESTABLECIMIENTO_PRODUCTO" product
                                 WHERE product.establecimiento_id = establishment.id AND product.activo = true),
                                establishment.produccion_anual / 12) AS MonthlyProduction,
                       establishment.haccp_implementado AS HaccpImplemented,
                       establishment.nivel_haccp_porcentaje AS HaccpPercentage,
                       establishment.es_suplidor_inabie AS IsInabieSupplier,
                       establishment.distribucion_inabie_codigo AS InabieDistributionCode,
                       establishment.rechazos_microbiologicos_ultimos_5_anios AS MicrobiologicalRejectionsLastFiveYears,
                       establishment.plan_muestreo_microbiologico AS MicrobiologicalSamplingPlan,
                       establishment.aplicacion_muestreo_codigo AS SamplingApplicationCode
                FROM "SIGERSA"."EVALUACION" evaluation
                JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
                WHERE evaluation.id = @EvaluationId AND {AccessPredicate};
                """;
            var header = await connection.QuerySingleOrDefaultAsync<CalculationHeaderRow>(new CommandDefinition(Sql(evaluationSql), new { EvaluationId = evaluationId, ActorId = actorId }, cancellationToken: cancellationToken));
            if (header is null) throw new KeyNotFoundException("La evaluación no existe o no está disponible para el usuario.");
            var items = await GetFormAsync(evaluationId, actorId, cancellationToken);
            var answers = (await connection.QueryAsync<AnswerInputRow>(new CommandDefinition(Sql("""
                SELECT response.id AS Id, response.evaluacion_id AS EvaluationId,
                       item.source_allitems_item AS SourceItem, response.valor_texto AS Rating,
                       criticality.codigo AS CriticalityCode,
                       response.puntaje_obtenido AS Score, response.version_fila AS RowVersion
                FROM "SIGERSA"."RESPUESTA_USUARIO" response
                JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
                LEFT JOIN "SIGERSA"."NIVEL_CRITICIDAD" criticality ON criticality.id = response.nivel_criticidad_id
                WHERE response.evaluacion_id = @EvaluationId;
                """), new { EvaluationId = evaluationId }, cancellationToken: cancellationToken))).Select(row => row.ToDomain()).ToArray();
            return new EvaluationCalculationInput(evaluationId, header.RowVersion, items, answers,
                header.ProductRisk,
                header.MonthlyProduction, header.HaccpImplemented, header.HaccpPercentage,
                header.IsInabieSupplier, header.InabieDistributionCode,
                header.MicrobiologicalRejectionsLastFiveYears,
                header.MicrobiologicalSamplingPlan, header.SamplingApplicationCode);
        }
    }

    public async Task<long> SaveCalculationAsync(EvaluationCalculation calculation, decimal obtainedScore, decimal applicableScore, string snapshotJson, string snapshotHash, long expectedVersion, Guid actorId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH updated AS (
            UPDATE "SIGERSA"."EVALUACION"
            SET porcentaje_cumplimiento = @CompliancePercentage,
                puntaje_obtenido = @ObtainedScore, puntaje_aplicable = @ApplicableScore,
                riesgo_producto = @ProductRisk, riesgo_establecimiento = @EstablishmentRisk,
                riesgo_total = @TotalRisk, nivel_riesgo = @RiskLevel,
                frecuencia = @DatabaseFrequency, snapshot_calculo = @Snapshot::jsonb,
                hash_calculo = @Hash, modificado_por = @ActorId,
                modificado_en = CURRENT_TIMESTAMP, version_fila = version_fila + 1
            WHERE id = @EvaluationId AND version_fila = @ExpectedVersion
              AND estado = 'EN_EJECUCION'
            RETURNING version_fila, caso_id, evaluador_principal_id
            ), next_case AS (
                UPDATE "SIGERSA"."CASO" inspection_case
                   SET proxima_inspeccion_en = @NextInspectionDate::date,
                       modificado_por = @ActorId, modificado_en = CURRENT_TIMESTAMP,
                       version_fila = inspection_case.version_fila + 1
                  FROM updated
                 WHERE inspection_case.id = updated.caso_id
                RETURNING inspection_case.id
            ), retired_reminders AS (
                UPDATE "SIGERSA"."NOTIFICACION" notification
                   SET estado = 'LEIDA', leida_en = COALESCE(leida_en, CURRENT_TIMESTAMP),
                       modificado_por = @ActorId, modificado_en = CURRENT_TIMESTAMP,
                       version_fila = version_fila + 1
                 WHERE notification.recurso_tipo = 'EVALUACION'
                   AND notification.recurso_id = @EvaluationId
                   AND notification.tipo LIKE 'RECORDATORIO_INSPECCION_%'
                   AND notification.leida_en IS NULL
                RETURNING notification.id
            ), recipients AS (
                SELECT evaluador_principal_id AS usuario_id FROM updated
                UNION
                SELECT user_role.usuario_id
                  FROM "SIGERSA"."USUARIO_ROL" user_role
                  JOIN "SIGERSA"."ROL" role ON role.id = user_role.rol_id
                  JOIN "SIGERSA"."USUARIO" app_user ON app_user.id = user_role.usuario_id
                 WHERE user_role.activo = true AND role.activo = true AND app_user.activo = true
                   AND role.codigo IN ('ADMINISTRADOR', 'COORDINADOR')
                   AND user_role.vigente_desde <= CURRENT_TIMESTAMP
                   AND (user_role.vigente_hasta IS NULL OR user_role.vigente_hasta > CURRENT_TIMESTAMP)
            ), reminders AS (
                SELECT * FROM (VALUES
                    (30, '30_DIAS', 'La próxima inspección es en 30 días.'),
                    (14, '14_DIAS', 'La próxima inspección es en 14 días.'),
                    (7, '7_DIAS', 'La próxima inspección es en 7 días.'),
                    (0, 'HOY', 'La inspección programada corresponde al día de hoy.')
                ) value(days_before, code, message)
            ), inserted_reminders AS (
                INSERT INTO "SIGERSA"."NOTIFICACION"
                    (id, usuario_id, tipo, titulo, mensaje, canal, estado,
                     recurso_tipo, recurso_id, programada_para, creado_por)
                SELECT gen_random_uuid(), recipients.usuario_id,
                       'RECORDATORIO_INSPECCION_' || reminders.code,
                       'Recordatorio de próxima inspección', reminders.message,
                       'INTERNA', 'PENDIENTE', 'EVALUACION', @EvaluationId,
                       (@NextInspectionDate::date - reminders.days_before)::timestamptz, @ActorId
                  FROM recipients CROSS JOIN reminders
                 WHERE @NextInspectionDate IS NOT NULL
                ON CONFLICT DO NOTHING
                RETURNING id
            )
            SELECT version_fila FROM updated;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var version = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(Sql(sql), new { calculation.EvaluationId, calculation.CompliancePercentage, ObtainedScore = obtainedScore, ApplicableScore = applicableScore, calculation.ProductRisk, calculation.EstablishmentRisk, calculation.TotalRisk, calculation.RiskLevel, NextInspectionDate = calculation.NextInspectionDate?.ToDateTime(TimeOnly.MinValue), DatabaseFrequency = calculation.Frequency == "NO_APLICA" ? null : calculation.Frequency, Snapshot = snapshotJson, Hash = snapshotHash, ActorId = actorId, ExpectedVersion = expectedVersion }, cancellationToken: cancellationToken));
            if (version is null) throw new OptimisticConcurrencyException(calculation.EvaluationId);
            return version.Value;
        }
    }

    private static async Task<EvaluationAnswer> ReadAnswerAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, Guid id, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleAsync<AnswerDetailRow>(new CommandDefinition(Sql("""
            SELECT response.id AS Id, response.evaluacion_id AS EvaluationId,
                   item.source_allitems_item AS SourceItem, response.valor_texto AS Rating,
                   criticality.codigo AS CriticalityCode,
                   response.puntaje_obtenido AS Score, response.version_fila AS RowVersion
            FROM "SIGERSA"."RESPUESTA_USUARIO" response
            JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
            LEFT JOIN "SIGERSA"."NIVEL_CRITICIDAD" criticality ON criticality.id = response.nivel_criticidad_id
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

    private static EvaluationSupplement EmptySupplement() =>
        new(null, null, null, null, null, null, null, null, [], [], 0);

    private sealed class AllItemsRow { public int Items { get; init; } public string ItemsId { get; init; } = string.Empty; public string Description { get; init; } = string.Empty; public string SectionType { get; init; } = string.Empty; public string? Parents { get; init; } }
    private sealed class PreviousTemplateRow { public Guid Id { get; init; } public Guid RootId { get; init; } public int Version { get; init; } }
    private sealed class SyncOperationRow { public string PayloadHash { get; init; } = string.Empty; public Guid? ResourceId { get; init; } public string Status { get; init; } = string.Empty; }
    private sealed class AnswerRow { public Guid Id { get; init; } public long RowVersion { get; init; } }
    private sealed class AnswerInputRow { public Guid Id { get; init; } public Guid EvaluationId { get; init; } public int SourceItem { get; init; } public string Rating { get; init; } = string.Empty; public string? CriticalityCode { get; init; } public decimal? Score { get; init; } public long RowVersion { get; init; } public EvaluationAnswer ToDomain() => new(Id, EvaluationId, SourceItem, Rating, CriticalityCode, Score, RowVersion); }
    private sealed class AnswerDetailRow { public Guid Id { get; init; } public Guid EvaluationId { get; init; } public int SourceItem { get; init; } public string Rating { get; init; } = string.Empty; public string? CriticalityCode { get; init; } public decimal? Score { get; init; } public long RowVersion { get; init; } public EvaluationAnswer ToDomain() => new(Id, EvaluationId, SourceItem, Rating, CriticalityCode, Score, RowVersion); }
    private sealed class EvaluationSavedEvidenceRow
    {
        public Guid Id { get; init; }
        public int SourceItem { get; init; }
        public string OriginalName { get; init; } = string.Empty;
        public DateTime UploadedAt { get; init; }
        public EvaluationSavedEvidence ToDomain() => new(
            Id, SourceItem, OriginalName,
            new DateTimeOffset(DateTime.SpecifyKind(UploadedAt, DateTimeKind.Utc)));
    }
    private sealed class InspectionContextRow
    {
        public string Origin { get; init; } = string.Empty;
        public string? InspectionReasonCode { get; init; }
        public string? InspectionReasonName { get; init; }
        public bool HasValidSanitaryPermit { get; init; }
        public Guid? PreviousEvaluationId { get; init; }
        public DateOnly? PreviousInspectionDate { get; init; }
        public decimal? PreviousCompliancePercentage { get; init; }
    }
    private sealed class CalculationHeaderRow { public long RowVersion { get; init; } public decimal? ProductRisk { get; init; } public decimal? MonthlyProduction { get; init; } public bool? HaccpImplemented { get; init; } public decimal? HaccpPercentage { get; init; } public bool? IsInabieSupplier { get; init; } public string? InabieDistributionCode { get; init; } public int MicrobiologicalRejectionsLastFiveYears { get; init; } public bool? MicrobiologicalSamplingPlan { get; init; } public string? SamplingApplicationCode { get; init; } }
    private sealed class EvaluationSupplementRow
    {
        public DateOnly? PreviousInspectionDate { get; init; }
        public string? PreviousQualification { get; init; }
        public DateOnly? CurrentInspectionDate { get; init; }
        public string? CurrentQualification { get; init; }
        public string? DpsDasOfficer1 { get; init; }
        public string? DpsDasOfficer2 { get; init; }
        public string? DigemapsTechnician1 { get; init; }
        public string? DigemapsTechnician2 { get; init; }
        public string CorrectiveMeasuresJson { get; init; } = "[]";
        public string RecommendationsJson { get; init; } = "[]";
        public long RowVersion { get; init; }

        public EvaluationSupplement ToDomain() => new(
            PreviousInspectionDate,
            PreviousQualification,
            CurrentInspectionDate,
            CurrentQualification,
            DpsDasOfficer1,
            DpsDasOfficer2,
            DigemapsTechnician1,
            DigemapsTechnician2,
            JsonSerializer.Deserialize<EvaluationFollowUpItem[]>(CorrectiveMeasuresJson) ?? [],
            JsonSerializer.Deserialize<EvaluationFollowUpItem[]>(RecommendationsJson) ?? [],
            RowVersion);
    }
    private sealed class EvaluationSummaryRow
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public Guid CaseId { get; init; }
        public string CaseNumber { get; init; } = string.Empty;
        public Guid EstablishmentId { get; init; }
        public string EstablishmentName { get; init; } = string.Empty;
        public Guid EvaluatorId { get; init; }
        public string EvaluatorName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty; public DateTime? ScheduledStart { get; init; }
        public DateTime? ScheduledEnd { get; init; }
        public decimal? CompliancePercentage { get; init; }
        public decimal? TotalRisk { get; init; }
        public string? RiskLevel { get; init; }
        public string? Frequency { get; init; }
        public DateTime? NextInspectionAt { get; init; }
        public int AnsweredItems { get; init; }
        public bool HasOfficialReport { get; init; }
        public bool CanEdit { get; init; }
        public long RowVersion { get; init; }
        public EvaluationSummary ToDomain() => new(Id, Number, CaseId, CaseNumber, EstablishmentId,
            EstablishmentName, EvaluatorId, EvaluatorName, Status, Utc(ScheduledStart), Utc(ScheduledEnd),
            CompliancePercentage, TotalRisk, RiskLevel, Frequency, Utc(NextInspectionAt), AnsweredItems,
            HasOfficialReport, CanEdit, RowVersion);
        private static DateTimeOffset? Utc(DateTime? value) => value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
    }

    public async Task<long?> TransitionAsync(
        Guid evaluationId,
        EvaluationTransitionDraft transition,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH RECURSIVE updated AS (
                UPDATE "SIGERSA"."EVALUACION" evaluation
                   SET estado = CASE @Action
                           WHEN 'START' THEN 'EN_EJECUCION'
                           WHEN 'FINALIZE' THEN 'FINALIZADA'
                           WHEN 'SUBMIT' THEN 'ENVIADA'
                           WHEN 'REVIEW' THEN 'EN_REVISION'
                           WHEN 'APPROVE' THEN 'APROBADA'
                           WHEN 'REJECT' THEN 'NO_APROBADA'
                           WHEN 'CLOSE' THEN 'CERRADA'
                           WHEN 'CANCEL' THEN 'CANCELADA'
                       END,
                       iniciada_en = CASE WHEN @Action = 'START' THEN CURRENT_TIMESTAMP ELSE iniciada_en END,
                       finalizada_en = CASE WHEN @Action = 'FINALIZE' THEN CURRENT_TIMESTAMP ELSE finalizada_en END,
                       enviada_en = CASE WHEN @Action = 'SUBMIT' THEN CURRENT_TIMESTAMP ELSE enviada_en END,
                       aprobada_en = CASE WHEN @Action = 'APPROVE' THEN CURRENT_TIMESTAMP ELSE aprobada_en END,
                       cerrada_en = CASE WHEN @Action = 'CLOSE' THEN CURRENT_TIMESTAMP ELSE cerrada_en END,
                       cancelada_en = CASE WHEN @Action = 'CANCEL' THEN CURRENT_TIMESTAMP ELSE cancelada_en END,
                       motivo_cancelacion = CASE WHEN @Action = 'CANCEL' THEN @Reason ELSE motivo_cancelacion END,
                       latitud_inicio = CASE WHEN @Action = 'START' THEN @Latitude ELSE latitud_inicio END,
                       longitud_inicio = CASE WHEN @Action = 'START' THEN @Longitude ELSE longitud_inicio END,
                       precision_m = CASE WHEN @Action = 'START' THEN @AccuracyMeters ELSE precision_m END,
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE evaluation.id = @EvaluationId AND evaluation.version_fila = @RowVersion
                   AND (
                       (@Action = 'START' AND evaluation.estado = 'ASIGNADA')
                       OR (@Action = 'FINALIZE' AND evaluation.estado = 'EN_EJECUCION'
                           AND evaluation.riesgo_total IS NOT NULL)
                       OR (@Action = 'SUBMIT' AND evaluation.estado = 'FINALIZADA')
                       OR (@Action = 'REVIEW' AND evaluation.estado = 'ENVIADA')
                       OR (@Action = 'APPROVE' AND evaluation.estado IN ('ENVIADA', 'EN_REVISION')
                           AND evaluation.evaluador_principal_id <> @ActorId)
                       OR (@Action = 'REJECT' AND evaluation.estado IN ('ENVIADA', 'EN_REVISION')
                           AND evaluation.evaluador_principal_id <> @ActorId)
                       OR (@Action = 'CLOSE'
                           AND evaluation.estado IN ('APROBADA', 'NO_APROBADA')
                           AND EXISTS (
                               SELECT 1 FROM "SIGERSA"."INFORME" report
                               JOIN "SIGERSA"."INFORME_VERSION" version ON version.informe_id = report.id
                               WHERE report.evaluacion_id = evaluation.id AND version.es_oficial = true))
                       OR (@Action = 'CANCEL'
                           AND evaluation.estado IN ('ASIGNADA', 'EN_EJECUCION', 'EN_CORRECCION',
                                                     'FINALIZADA', 'ENVIADA', 'EN_REVISION'))
                   )
                   AND (
                       (@Action IN ('START', 'FINALIZE', 'SUBMIT')
                        AND evaluation.evaluador_principal_id = @ActorId)
                       OR (@Action IN ('REVIEW', 'APPROVE', 'REJECT', 'CLOSE', 'CANCEL') AND EXISTS (
                            SELECT 1 FROM "SIGERSA"."USUARIO_ROL" user_role
                            JOIN "SIGERSA"."ROL" role ON role.id = user_role.rol_id
                            WHERE user_role.usuario_id = @ActorId AND user_role.activo = true
                              AND role.codigo = 'COORDINADOR'))
                   )
                RETURNING evaluation.version_fila AS RowVersion, evaluation.caso_id AS CaseId,
                          evaluation.programacion_id AS ScheduleId,
                          evaluation.id AS EvaluationId, evaluation.numero AS EvaluationNumber,
                          evaluation.establecimiento_id AS EstablishmentId,
                          evaluation.ficha_inspeccion_id AS InspectionTemplateId,
                          evaluation.evaluador_principal_id AS EvaluatorId,
                          evaluation.version_regla_riesgo_id AS RiskRuleVersionId
            ), inspection_lineage AS (
                SELECT (inspection_case.metadatos_origen ->> 'evaluacionAnteriorId')::uuid AS evaluation_id
                  FROM updated
                  JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = updated.CaseId
                 WHERE inspection_case.metadatos_origen ->> 'tipo' = 'REINSPECCION_NO_CONFORMIDADES'
                   AND inspection_case.metadatos_origen ->> 'evaluacionAnteriorId' IS NOT NULL
                UNION ALL
                SELECT (ancestor_case.metadatos_origen ->> 'evaluacionAnteriorId')::uuid
                  FROM inspection_lineage lineage
                  JOIN "SIGERSA"."EVALUACION" ancestor ON ancestor.id = lineage.evaluation_id
                  JOIN "SIGERSA"."CASO" ancestor_case ON ancestor_case.id = ancestor.caso_id
                 WHERE ancestor_case.metadatos_origen ->> 'tipo' = 'REINSPECCION_NO_CONFORMIDADES'
                   AND ancestor_case.metadatos_origen ->> 'evaluacionAnteriorId' IS NOT NULL
            ), progressed_ancestor_findings AS (
                UPDATE "SIGERSA"."NO_CONFORMIDAD" nonconformity
                   SET estado = CASE WHEN @Action = 'START' THEN 'EN_CORRECCION' ELSE 'CERRADO' END,
                       cerrada_en = CASE WHEN @Action = 'CLOSE' THEN CURRENT_TIMESTAMP ELSE NULL END,
                       cierre_justificacion = CASE WHEN @Action = 'CLOSE'
                           THEN 'Cerrada automáticamente al cerrar la reinspección que validó el cumplimiento.'
                           ELSE NULL END,
                       modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId,
                       version_fila = nonconformity.version_fila + 1
                 WHERE nonconformity.evaluacion_id IN (SELECT evaluation_id FROM inspection_lineage)
                   AND ((@Action = 'START' AND nonconformity.estado = 'ABIERTA')
                        OR (@Action = 'CLOSE' AND nonconformity.estado = 'VALIDADO'))
                RETURNING nonconformity.id
            ), follow_up_seed AS MATERIALIZED (
                SELECT gen_random_uuid() AS FollowUpCaseId,
                       gen_random_uuid() AS FollowUpScheduleId,
                       gen_random_uuid() AS FollowUpEvaluationId,
                       updated.CaseId AS PreviousCaseId,
                       updated.EvaluationId AS PreviousEvaluationId,
                       updated.EvaluationNumber, updated.EstablishmentId,
                       updated.InspectionTemplateId, updated.EvaluatorId,
                       updated.RiskRuleVersionId,
                       inspection_case.empresa_id AS CompanyId,
                       CASE WHEN EXISTS (
                           SELECT 1
                             FROM "SIGERSA"."NO_CONFORMIDAD" nonconformity
                             JOIN "SIGERSA"."NIVEL_CRITICIDAD" criticality
                               ON criticality.id = nonconformity.nivel_criticidad_id
                            WHERE nonconformity.evaluacion_id = updated.EvaluationId
                              AND nonconformity.estado <> 'CERRADO'
                              AND criticality.codigo = 'C'
                       ) THEN 1 ELSE 2 END::smallint AS Priority,
                       CURRENT_TIMESTAMP + INTERVAL '30 days' AS StartsAt
                  FROM updated
                  JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = updated.CaseId
                 WHERE @Action = 'CLOSE'
                   AND EXISTS (
                       SELECT 1 FROM "SIGERSA"."NO_CONFORMIDAD" nonconformity
                        WHERE nonconformity.evaluacion_id = updated.EvaluationId
                          AND nonconformity.estado <> 'CERRADO')
            ), follow_up_case AS (
                INSERT INTO "SIGERSA"."CASO"
                    (id, numero, empresa_id, establecimiento_id, motivo_inspeccion_id,
                     origen, estado, prioridad, responsable_actual_id, motivo_decision,
                     metadatos_origen, creado_por)
                SELECT seed.FollowUpCaseId,
                       'CAS-' || to_char(CURRENT_TIMESTAMP, 'YYYYMMDD') || '-' ||
                           upper(substr(replace(seed.FollowUpCaseId::text, '-', ''), 1, 8)),
                       seed.CompanyId, seed.EstablishmentId, reason.id,
                       'PROGRAMACION', 'PROGRAMADO', seed.Priority, @ActorId,
                       'Reinspección automática por no conformidades de ' || seed.EvaluationNumber,
                       jsonb_build_object('evaluacionAnteriorId', seed.PreviousEvaluationId,
                                          'tipo', 'REINSPECCION_NO_CONFORMIDADES'),
                       @ActorId
                  FROM follow_up_seed seed
                  JOIN "SIGERSA"."MOTIVO_INSPECCION" reason
                    ON reason.codigo = 'INSPECCION_CONTROL' AND reason.activo = true
                RETURNING id
            ), follow_up_schedule AS (
                INSERT INTO "SIGERSA"."PROGRAMACION"
                    (id, caso_id, ficha_inspeccion_id, inicio_programado, fin_programado,
                     prioridad, estado, observaciones, programado_por, creado_por)
                SELECT seed.FollowUpScheduleId, seed.FollowUpCaseId, seed.InspectionTemplateId,
                       seed.StartsAt, seed.StartsAt + INTERVAL '4 hours', seed.Priority,
                       'PROGRAMADA',
                       'Reinspección automática; auditar únicamente las no conformidades de ' ||
                           seed.EvaluationNumber,
                       @ActorId, @ActorId
                  FROM follow_up_seed seed
                  JOIN follow_up_case created_case ON created_case.id = seed.FollowUpCaseId
                RETURNING id
            ), follow_up_assignment AS (
                INSERT INTO "SIGERSA"."ASIGNACION"
                    (id, programacion_id, evaluador_id, es_principal, estado,
                     motivo_asignacion, asignado_por, creado_por)
                SELECT gen_random_uuid(), seed.FollowUpScheduleId, seed.EvaluatorId, true, 'ACTIVA',
                       'Reinspección automática por no conformidades', @ActorId, @ActorId
                  FROM follow_up_seed seed
                  JOIN follow_up_schedule schedule ON schedule.id = seed.FollowUpScheduleId
                RETURNING programacion_id
            ), follow_up_evaluation AS (
                INSERT INTO "SIGERSA"."EVALUACION"
                    (id, numero, caso_id, programacion_id, establecimiento_id,
                     ficha_inspeccion_id, evaluador_principal_id, estado, alcance,
                     programada_inicio_en, programada_fin_en, version_regla_riesgo_id,
                     creado_por)
                SELECT seed.FollowUpEvaluationId,
                       'EV-' || to_char(CURRENT_TIMESTAMP, 'YYYYMMDD') || '-' ||
                           upper(substr(replace(seed.FollowUpEvaluationId::text, '-', ''), 1, 8)),
                       seed.FollowUpCaseId, seed.FollowUpScheduleId, seed.EstablishmentId,
                       seed.InspectionTemplateId, seed.EvaluatorId, 'ASIGNADA', 'SEGUIMIENTO',
                       seed.StartsAt, seed.StartsAt + INTERVAL '4 hours',
                       seed.RiskRuleVersionId, @ActorId
                  FROM follow_up_seed seed
                  JOIN follow_up_assignment assignment
                    ON assignment.programacion_id = seed.FollowUpScheduleId
                RETURNING id
            ), follow_up_history AS (
                INSERT INTO "SIGERSA"."PROGRAMACION_HISTORIAL"
                    (id, programacion_id, accion, inicio_nuevo, fin_nuevo, prioridad_nueva,
                     estado_nuevo, motivo, realizado_por, creado_por)
                SELECT gen_random_uuid(), seed.FollowUpScheduleId, 'CREADA', seed.StartsAt,
                       seed.StartsAt + INTERVAL '4 hours', seed.Priority, 'PROGRAMADA',
                       'Reinspección automática por no conformidades', @ActorId, @ActorId
                  FROM follow_up_seed seed
                  JOIN follow_up_evaluation evaluation ON evaluation.id = seed.FollowUpEvaluationId
                RETURNING id
            ), request_in_progress AS (
                UPDATE "SIGERSA"."SOLICITUD" request
                   SET estado = 'EN_PROCESO', modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId, version_fila = request.version_fila + 1
                  FROM "SIGERSA"."CASO" inspection_case
                 WHERE @Action = 'START' AND inspection_case.id IN (SELECT CaseId FROM updated)
                   AND request.id = inspection_case.solicitud_id
                   AND request.estado IN ('PENDIENTE_ASIGNACION', 'ASIGNADA')
                RETURNING request.id
            ), previous_schedule AS MATERIALIZED (
                SELECT schedule.id, schedule.inicio_programado AS StartsAt,
                       schedule.fin_programado AS EndsAt, schedule.prioridad AS Priority,
                       schedule.estado AS PreviousStatus
                  FROM "SIGERSA"."PROGRAMACION" schedule
                 WHERE @Action = 'CANCEL' AND schedule.id IN (SELECT ScheduleId FROM updated)
                   AND schedule.estado IN ('PROGRAMADA', 'REPROGRAMADA')
            ), cancelled_schedule AS (
                UPDATE "SIGERSA"."PROGRAMACION" schedule
                   SET estado = 'CANCELADA', motivo_cambio = @Reason,
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE schedule.id IN (SELECT id FROM previous_schedule)
                RETURNING schedule.id
            ), cancelled_assignments AS (
                UPDATE "SIGERSA"."ASIGNACION" assignment
                   SET estado = 'REVOCADA', revocado_en = CURRENT_TIMESTAMP,
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE @Action = 'CANCEL'
                   AND assignment.programacion_id IN (SELECT id FROM cancelled_schedule)
                   AND assignment.estado = 'ACTIVA'
                RETURNING assignment.id
            ), cancellation_history AS (
                INSERT INTO "SIGERSA"."PROGRAMACION_HISTORIAL"
                    (id, programacion_id, accion, inicio_anterior, fin_anterior,
                     prioridad_anterior, estado_anterior, estado_nuevo, motivo,
                     realizado_por, creado_por)
                SELECT gen_random_uuid(), schedule.id, 'CANCELADA', schedule.StartsAt,
                       schedule.EndsAt, schedule.Priority, schedule.PreviousStatus, 'CANCELADA',
                       @Reason, @ActorId, @ActorId
                  FROM previous_schedule schedule
                  JOIN cancelled_schedule cancelled ON cancelled.id = schedule.id
                RETURNING id
            ), closed_case AS (
                UPDATE "SIGERSA"."CASO" inspection_case
                   SET estado = 'CERRADO', cerrado_en = CURRENT_TIMESTAMP,
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE @Action = 'CLOSE' AND inspection_case.id IN (SELECT CaseId FROM updated)
                RETURNING inspection_case.id
            ), completed_schedule AS (
                UPDATE "SIGERSA"."PROGRAMACION" schedule
                   SET estado = 'COMPLETADA', modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId, version_fila = version_fila + 1
                 WHERE @Action = 'CLOSE' AND schedule.id IN (SELECT ScheduleId FROM updated)
                   AND schedule.estado IN ('PROGRAMADA', 'REPROGRAMADA')
                RETURNING schedule.id
            ), resolved_request AS (
                UPDATE "SIGERSA"."SOLICITUD" request
                   SET estado = 'RESUELTA', modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId, version_fila = request.version_fila + 1
                  FROM "SIGERSA"."CASO" inspection_case
                 WHERE @Action = 'CLOSE' AND inspection_case.id IN (SELECT CaseId FROM updated)
                   AND request.id = inspection_case.solicitud_id
                   AND request.estado NOT IN ('CANCELADA', 'RECHAZADA', 'RESUELTA')
                RETURNING request.id
            )
            SELECT RowVersion FROM updated;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(Sql(sql), new
            {
                EvaluationId = evaluationId,
                transition.Action,
                transition.RowVersion,
                transition.Latitude,
                transition.Longitude,
                transition.AccuracyMeters,
                transition.Reason,
                ActorId = actorId
            }, cancellationToken: cancellationToken));
        }
    }
}
