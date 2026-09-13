using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class CorrectionRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), ICorrectionRepository
{
    public async Task<CorrectionsPage> SearchAsync(CorrectionSearch query, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT correction.id AS Id, correction.evaluacion_id AS EvaluationId,
                   evaluation.numero AS EvaluationNumber, establishment.nombre AS EstablishmentName,
                   correction.numero_revision AS RevisionNumber,
                   correction.tipo_responsable AS ResponsibleType,
                   correction.asignada_a_id AS AssignedToId, assigned_user.nombre_completo AS AssignedToName,
                   correction.observacion_coordinador AS CoordinatorObservation,
                   correction.estado AS Status, correction.fecha_limite AS DueAt,
                   correction.solicitada_en AS RequestedAt, correction.enviada_en AS SubmittedAt,
                   correction.resuelta_en AS ResolvedAt,
                   COALESCE((SELECT jsonb_agg(jsonb_build_object(
                       'SourceItem', item.source_allitems_item, 'ItemTitle', item.titulo,
                       'Reason', field.motivo, 'Status', field.estado,
                       'PreviousSnapshotJson', field.valor_anterior_snapshot::text,
                       'NewSnapshotJson', field.valor_nuevo_snapshot::text) ORDER BY item.orden)::text
                     FROM "SIGERSA"."CORRECCION_CAMPO" field
                     JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = field.item_ficha_id
                    WHERE field.correccion_id = correction.id), '[]') AS FieldsJson,
                   correction.version_fila AS RowVersion
              FROM "SIGERSA"."CORRECCION" correction
              JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.id = correction.evaluacion_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
              LEFT JOIN "SIGERSA"."USUARIO" assigned_user ON assigned_user.id = correction.asignada_a_id
             WHERE (@Status IS NULL OR correction.estado = @Status)
               AND (@ReviewScope IS NULL
                    OR (@ReviewScope = 'COORDINADOR' AND correction.estado IN ('PENDIENTE', 'ENVIADA'))
                    OR (@ReviewScope = 'ADMINISTRADOR' AND correction.estado IN ('EN_PROCESO', 'ACEPTADA')))
               AND (@Search IS NULL OR lower(evaluation.numero) LIKE '%' || @Search || '%'
                    OR lower(establishment.nombre) LIKE '%' || @Search || '%'
                    OR lower(correction.observacion_coordinador) LIKE '%' || @Search || '%')
               AND (@GlobalScope = true
                    OR (@CompanyScope IS NOT NULL AND establishment.empresa_id = @CompanyScope
                        AND correction.tipo_responsable = 'EMPRESA')
                    OR (@AssignedOnly = true AND correction.tipo_responsable = 'TECNICO'
                        AND (correction.asignada_a_id = @ActorId
                             OR evaluation.evaluador_principal_id = @ActorId
                             OR EXISTS (SELECT 1 FROM "SIGERSA"."ASIGNACION" assignment
                                  WHERE assignment.programacion_id = evaluation.programacion_id
                                    AND assignment.evaluador_id = @ActorId AND assignment.estado = 'ACTIVA'))))
             ORDER BY correction.solicitada_en DESC, correction.id
             OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
              FROM "SIGERSA"."CORRECCION" correction
              JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.id = correction.evaluacion_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
             WHERE (@Status IS NULL OR correction.estado = @Status)
               AND (@ReviewScope IS NULL
                    OR (@ReviewScope = 'COORDINADOR' AND correction.estado IN ('PENDIENTE', 'ENVIADA'))
                    OR (@ReviewScope = 'ADMINISTRADOR' AND correction.estado IN ('EN_PROCESO', 'ACEPTADA')))
               AND (@Search IS NULL OR lower(evaluation.numero) LIKE '%' || @Search || '%'
                    OR lower(establishment.nombre) LIKE '%' || @Search || '%'
                    OR lower(correction.observacion_coordinador) LIKE '%' || @Search || '%')
               AND (@GlobalScope = true
                    OR (@CompanyScope IS NOT NULL AND establishment.empresa_id = @CompanyScope
                        AND correction.tipo_responsable = 'EMPRESA')
                    OR (@AssignedOnly = true AND correction.tipo_responsable = 'TECNICO'
                        AND (correction.asignada_a_id = @ActorId
                             OR evaluation.evaluador_principal_id = @ActorId
                             OR EXISTS (SELECT 1 FROM "SIGERSA"."ASIGNACION" assignment
                                  WHERE assignment.programacion_id = evaluation.programacion_id
                                    AND assignment.evaluador_id = @ActorId AND assignment.estado = 'ACTIVA'))));
            """;
        var parameters = new
        {
            query.Search, query.Status, query.ActorId, query.CompanyScope,
            query.GlobalScope, query.AssignedOnly, query.ReviewScope,
            Offset = (query.Page - 1) * query.PageSize, query.PageSize
        };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(Sql(sql), parameters, cancellationToken: cancellationToken));
            var items = (await result.ReadAsync<CorrectionRow>()).Select(row => row.ToDomain()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new CorrectionsPage(items, query.Page, query.PageSize, total);
        }
    }

    public async Task<CorrectionOptions> GetOptionsAsync(Guid actorId, bool canCreate, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT evaluation.id AS Id, evaluation.numero || ' · ' || establishment.nombre AS Name
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
             WHERE evaluation.estado = 'EN_EJECUCION'
               AND (evaluation.evaluador_principal_id = @ActorId
                    OR EXISTS (
                        SELECT 1 FROM "SIGERSA"."ASIGNACION" assignment
                         WHERE assignment.programacion_id = evaluation.programacion_id
                           AND assignment.evaluador_id = @ActorId
                           AND assignment.estado = 'ACTIVA'))
               AND @CanCreate = true
             ORDER BY evaluation.creado_en DESC;

            SELECT user_account.id AS Id, user_account.nombre_completo AS Name
              FROM "SIGERSA"."USUARIO" user_account
             WHERE user_account.activo = true AND user_account.estado = 'ACTIVO'
               AND user_account.id = @ActorId AND @CanCreate = true
             ORDER BY user_account.nombre_completo;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(Sql(sql), new { ActorId = actorId, CanCreate = canCreate }, cancellationToken: cancellationToken));
            var evaluations = (await result.ReadAsync<CorrectionOption>()).AsList();
            var technicians = (await result.ReadAsync<CorrectionOption>()).AsList();
            return new CorrectionOptions(evaluations, technicians, canCreate);
        }
    }

    public async Task<Guid> CreateAsync(CorrectionDraft draft, Guid actorId, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(draft);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var insertedOperation = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."OPERACION_SINCRONIZACION"
                    (id, usuario_id, evaluacion_id, idempotency_key, dispositivo_id,
                     secuencia_cliente, tipo_operacion, recurso_tipo, payload, payload_hash,
                     estado, fecha_cliente, creado_por)
                VALUES (@OperationId, @ActorId, @EvaluationId, @Key, @Key, 0,
                        'CREAR_CORRECCION', 'CORRECCION', CAST(@Payload AS jsonb), @Hash,
                        'RECIBIDA', CURRENT_TIMESTAMP, @ActorId)
                ON CONFLICT (idempotency_key) DO NOTHING;
                """), new { OperationId = Guid.NewGuid(), ActorId = actorId, draft.EvaluationId, Key = draft.IdempotencyKey, Payload = payload, Hash = hash }, transaction, cancellationToken: cancellationToken));
            if (insertedOperation == 0)
            {
                var existing = await connection.QuerySingleAsync<OperationRow>(new CommandDefinition(Sql("""
                    SELECT recurso_id AS ResourceId, payload_hash AS PayloadHash
                      FROM "SIGERSA"."OPERACION_SINCRONIZACION" WHERE idempotency_key = @Key;
                    """), new { Key = draft.IdempotencyKey }, transaction, cancellationToken: cancellationToken));
                if (!string.Equals(existing.PayloadHash, hash, StringComparison.Ordinal) || !existing.ResourceId.HasValue)
                    throw new InvalidOperationException("La clave de idempotencia ya fue utilizada con otros datos.");
                await transaction.CommitAsync(cancellationToken);
                return existing.ResourceId.Value;
            }

            var evaluationExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(Sql("""
                SELECT EXISTS (
                    SELECT 1 FROM "SIGERSA"."EVALUACION" evaluation
                     WHERE evaluation.id = @EvaluationId
                       AND evaluation.estado = 'EN_EJECUCION'
                       AND (@ResponsibleType = 'EMPRESA'
                            OR (@ResponsibleType = 'TECNICO' AND @AssignedToId IS NOT NULL AND (
                                evaluation.evaluador_principal_id = @AssignedToId
                                OR EXISTS (SELECT 1 FROM "SIGERSA"."ASIGNACION" assignment
                                    WHERE assignment.programacion_id = evaluation.programacion_id
                                      AND assignment.evaluador_id = @AssignedToId AND assignment.estado = 'ACTIVA'))))
                    FOR UPDATE
                );
                """), new { draft.EvaluationId, draft.ResponsibleType, draft.AssignedToId }, transaction, cancellationToken: cancellationToken));
            if (!evaluationExists) throw new ArgumentException("La evaluación o el responsable seleccionado no está disponible.");

            var revision = await connection.ExecuteScalarAsync<int>(new CommandDefinition(Sql("""
                SELECT COALESCE(MAX(numero_revision), 0) + 1 FROM "SIGERSA"."CORRECCION" WHERE evaluacion_id = @EvaluationId;
                """), new { draft.EvaluationId }, transaction, cancellationToken: cancellationToken));
            var id = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."CORRECCION"
                    (id, evaluacion_id, numero_revision, tipo_responsable, asignada_a_id,
                     observacion_coordinador, estado, fecha_limite, solicitada_por, enviada_en, creado_por)
                VALUES (@Id, @EvaluationId, @Revision, @ResponsibleType, @AssignedToId,
                        @CoordinatorObservation, 'ENVIADA', @DueAt, @ActorId, CURRENT_TIMESTAMP, @ActorId);
                UPDATE "SIGERSA"."EVALUACION" SET estado = 'EN_CORRECCION',
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE id = @EvaluationId AND estado NOT IN ('CERRADA', 'CANCELADA');
                UPDATE "SIGERSA"."OPERACION_SINCRONIZACION" SET recurso_id = @Id,
                       estado = 'APLICADA', codigo_resultado = 'CREATED', procesada_en = CURRENT_TIMESTAMP,
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE idempotency_key = @Key;
                """), new { Id = id, draft.EvaluationId, Revision = revision, draft.ResponsibleType, draft.AssignedToId, draft.CoordinatorObservation, draft.DueAt, ActorId = actorId, Key = draft.IdempotencyKey }, transaction, cancellationToken: cancellationToken));
            foreach (var field in draft.Fields)
            {
                var insertedField = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                    INSERT INTO "SIGERSA"."CORRECCION_CAMPO"
                        (id, correccion_id, item_ficha_id, respuesta_usuario_id, motivo,
                         valor_anterior_snapshot, estado, creado_por)
                    SELECT gen_random_uuid(), @CorrectionId, item.id, response.id, @Reason,
                           jsonb_build_object('rating', response.valor_texto,
                                              'observation', response.observacion,
                                              'comment', response.comentario),
                           'PENDIENTE', @ActorId
                      FROM "SIGERSA"."EVALUACION" evaluation
                      JOIN "SIGERSA"."ITEM_FICHA" item
                        ON item.ficha_inspeccion_id = evaluation.ficha_inspeccion_id
                      LEFT JOIN "SIGERSA"."RESPUESTA_USUARIO" response
                        ON response.evaluacion_id = evaluation.id AND response.item_ficha_id = item.id
                     WHERE evaluation.id = @EvaluationId
                       AND item.source_allitems_item = @SourceItem AND item.es_evaluable = true;
                    """), new { CorrectionId = id, draft.EvaluationId, field.SourceItem, field.Reason,
                        ActorId = actorId }, transaction, cancellationToken: cancellationToken));
                if (insertedField != 1)
                    throw new ArgumentException($"El criterio observado {field.SourceItem} no pertenece a la evaluación.");
            }
            await transaction.CommitAsync(cancellationToken);
            return id;
        }
    }

    public async Task<bool> SubmitAsync(Guid id, long rowVersion, Guid actorId, Guid? companyScope, bool globalScope, bool assignedOnly, CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH submitted AS (
                UPDATE "SIGERSA"."CORRECCION" correction
                   SET estado = 'ENVIADA', enviada_en = CURRENT_TIMESTAMP,
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = correction.version_fila + 1
                  FROM "SIGERSA"."EVALUACION" evaluation,
                       "SIGERSA"."ESTABLECIMIENTO" establishment
                 WHERE correction.id = @Id AND correction.version_fila = @RowVersion
                   AND correction.estado IN ('PENDIENTE', 'EN_PROCESO')
                   AND evaluation.id = correction.evaluacion_id
                   AND establishment.id = evaluation.establecimiento_id
                   AND (@GlobalScope = true
                        OR (@CompanyScope IS NOT NULL AND correction.tipo_responsable = 'EMPRESA'
                            AND establishment.empresa_id = @CompanyScope)
                        OR (@AssignedOnly = true AND correction.tipo_responsable = 'TECNICO'
                            AND (correction.asignada_a_id = @ActorId OR evaluation.evaluador_principal_id = @ActorId)))
                RETURNING correction.id, correction.evaluacion_id
            ), updated_fields AS (
                UPDATE "SIGERSA"."CORRECCION_CAMPO" field
                   SET estado = 'CORREGIDO', valor_nuevo_snapshot = jsonb_build_object(
                           'rating', response.valor_texto, 'observation', response.observacion,
                           'comment', response.comentario),
                       modificado_por = @ActorId
                  FROM submitted, "SIGERSA"."RESPUESTA_USUARIO" response
                 WHERE field.correccion_id = submitted.id
                   AND response.evaluacion_id = submitted.evaluacion_id
                   AND response.item_ficha_id = field.item_ficha_id
                RETURNING field.id
            ), updated_evaluation AS (
                UPDATE "SIGERSA"."EVALUACION" evaluation
                   SET estado = 'EN_CORRECCION', modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId, version_fila = version_fila + 1
                 WHERE evaluation.id IN (SELECT evaluacion_id FROM submitted)
                RETURNING evaluation.id
            )
            SELECT COUNT(*) FROM submitted;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(Sql(sql), new { Id = id, RowVersion = rowVersion, ActorId = actorId, CompanyScope = companyScope, GlobalScope = globalScope, AssignedOnly = assignedOnly }, cancellationToken: cancellationToken)) == 1;
        }
    }

    public async Task<bool> ResolveAsync(Guid id, long rowVersion, string targetStatus, Guid actorId,
        bool coordinatorReview, CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH resolved AS (
                UPDATE "SIGERSA"."CORRECCION"
                   SET estado = CASE
                           WHEN @CoordinatorReview = true AND @TargetStatus = 'ACEPTADA' THEN 'EN_PROCESO'
                           ELSE @TargetStatus
                       END,
                       resuelta_en = CASE
                           WHEN @CoordinatorReview = true AND @TargetStatus = 'ACEPTADA' THEN NULL
                           ELSE CURRENT_TIMESTAMP
                       END,
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE id = @Id AND version_fila = @RowVersion
                   AND ((@CoordinatorReview = true AND estado IN ('PENDIENTE', 'ENVIADA'))
                        OR (@CoordinatorReview = false AND estado = 'EN_PROCESO'))
                RETURNING id, evaluacion_id
            ), updated_fields AS (
                UPDATE "SIGERSA"."CORRECCION_CAMPO" field
                   SET estado = CASE WHEN @TargetStatus = 'ACEPTADA' THEN 'ACEPTADO' ELSE 'RECHAZADO' END,
                       modificado_por = @ActorId
                 WHERE field.correccion_id IN (SELECT id FROM resolved)
                   AND NOT (@CoordinatorReview = true AND @TargetStatus = 'ACEPTADA')
                RETURNING field.id
            ), updated_evaluation AS (
                UPDATE "SIGERSA"."EVALUACION" evaluation
                   SET estado = CASE
                           WHEN @CoordinatorReview = true AND @TargetStatus = 'ACEPTADA' THEN 'EN_CORRECCION'
                           ELSE 'EN_EJECUCION'
                       END,
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE evaluation.id IN (SELECT evaluacion_id FROM resolved)
                   AND evaluation.estado = 'EN_CORRECCION'
                RETURNING evaluation.id
            )
            SELECT COUNT(*) FROM resolved;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(Sql(sql), new
            {
                Id = id, RowVersion = rowVersion, TargetStatus = targetStatus,
                ActorId = actorId, CoordinatorReview = coordinatorReview
            }, cancellationToken: cancellationToken)) == 1;
        }
    }

    private sealed class OperationRow { public Guid? ResourceId { get; init; } public string PayloadHash { get; init; } = string.Empty; }
    private sealed class CorrectionRow
    {
        public Guid Id { get; init; } public Guid EvaluationId { get; init; }
        public string EvaluationNumber { get; init; } = string.Empty; public string EstablishmentName { get; init; } = string.Empty;
        public int RevisionNumber { get; init; } public string ResponsibleType { get; init; } = string.Empty;
        public Guid? AssignedToId { get; init; } public string? AssignedToName { get; init; }
        public string CoordinatorObservation { get; init; } = string.Empty; public string Status { get; init; } = string.Empty;
        public DateTime DueAt { get; init; } public DateTime RequestedAt { get; init; }
        public DateTime? SubmittedAt { get; init; } public DateTime? ResolvedAt { get; init; } public long RowVersion { get; init; }
        public string FieldsJson { get; init; } = "[]";
        public CorrectionRecord ToDomain() => new(Id, EvaluationId, EvaluationNumber, EstablishmentName,
            RevisionNumber, ResponsibleType, AssignedToId, AssignedToName, CoordinatorObservation, Status,
            Utc(DueAt), Utc(RequestedAt), Utc(SubmittedAt), Utc(ResolvedAt),
            JsonSerializer.Deserialize<CorrectionField[]>(FieldsJson) ?? [], RowVersion);
        private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
        private static DateTimeOffset? Utc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;
    }
}
