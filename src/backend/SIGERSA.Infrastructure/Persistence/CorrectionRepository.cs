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
                   correction.resuelta_en AS ResolvedAt, correction.version_fila AS RowVersion
              FROM "SIGERSA"."CORRECCION" correction
              JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.id = correction.evaluacion_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
              LEFT JOIN "SIGERSA"."USUARIO" assigned_user ON assigned_user.id = correction.asignada_a_id
             WHERE (@Status IS NULL OR correction.estado = @Status)
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
            query.GlobalScope, query.AssignedOnly,
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

    public async Task<CorrectionOptions> GetOptionsAsync(bool canCreate, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT evaluation.id AS Id, evaluation.numero || ' · ' || establishment.nombre AS Name
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
             WHERE evaluation.estado NOT IN ('CERRADA', 'CANCELADA') AND @CanCreate = true
             ORDER BY evaluation.creado_en DESC;

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
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(Sql(sql), new { CanCreate = canCreate }, cancellationToken: cancellationToken));
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
                     WHERE evaluation.id = @EvaluationId AND evaluation.estado NOT IN ('CERRADA', 'CANCELADA')
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
                     observacion_coordinador, estado, fecha_limite, solicitada_por, creado_por)
                VALUES (@Id, @EvaluationId, @Revision, @ResponsibleType, @AssignedToId,
                        @CoordinatorObservation, 'PENDIENTE', @DueAt, @ActorId, @ActorId);
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
            await transaction.CommitAsync(cancellationToken);
            return id;
        }
    }

    public async Task<bool> SubmitAsync(Guid id, long rowVersion, Guid actorId, Guid? companyScope, bool globalScope, bool assignedOnly, CancellationToken cancellationToken = default)
    {
        const string sql = """
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
                        AND (correction.asignada_a_id = @ActorId OR evaluation.evaluador_principal_id = @ActorId)));
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.ExecuteAsync(new CommandDefinition(Sql(sql), new { Id = id, RowVersion = rowVersion, ActorId = actorId, CompanyScope = companyScope, GlobalScope = globalScope, AssignedOnly = assignedOnly }, cancellationToken: cancellationToken)) == 1;
        }
    }

    public async Task<bool> ResolveAsync(Guid id, long rowVersion, string targetStatus, Guid actorId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."CORRECCION"
               SET estado = @TargetStatus, resuelta_en = CURRENT_TIMESTAMP,
                   modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                   version_fila = version_fila + 1
             WHERE id = @Id AND version_fila = @RowVersion AND estado = 'ENVIADA';
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.ExecuteAsync(new CommandDefinition(Sql(sql), new { Id = id, RowVersion = rowVersion, TargetStatus = targetStatus, ActorId = actorId }, cancellationToken: cancellationToken)) == 1;
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
        public CorrectionRecord ToDomain() => new(Id, EvaluationId, EvaluationNumber, EstablishmentName,
            RevisionNumber, ResponsibleType, AssignedToId, AssignedToName, CoordinatorObservation, Status,
            Utc(DueAt), Utc(RequestedAt), Utc(SubmittedAt), Utc(ResolvedAt), RowVersion);
        private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
        private static DateTimeOffset? Utc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;
    }
}
