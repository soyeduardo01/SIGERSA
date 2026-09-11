using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class SchedulingRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), ISchedulingRepository
{
    public async Task<SchedulesPage> SearchAsync(ScheduleSearch query, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT p.id AS Id, p.caso_id AS CaseId, c.numero AS CaseNumber,
                   e.razon_social AS CompanyName, est.nombre AS EstablishmentName,
                   c.origen AS CaseOrigin, request.numero AS RequestNumber,
                   concat_ws(', ', NULLIF(est.calle, ''), NULLIF(est.numero_direccion, ''), municipality.nombre) AS Location,
                   p.inicio_programado AS StartsAt, p.fin_programado AS EndsAt,
                   p.prioridad AS Priority, p.estado AS Status, p.observaciones AS Observations,
                   p.motivo_cambio AS ChangeReason, p.version_fila AS RowVersion,
                   COALESCE(array_agg(a.evaluador_id ORDER BY a.es_principal DESC, u.nombre_completo)
                       FILTER (WHERE a.estado = 'ACTIVA'), ARRAY[]::uuid[]) AS EvaluatorIds,
                   COALESCE(array_agg(u.nombre_completo ORDER BY a.es_principal DESC, u.nombre_completo)
                       FILTER (WHERE a.estado = 'ACTIVA'), ARRAY[]::varchar[]) AS EvaluatorNames
              FROM "SIGERSA"."PROGRAMACION" AS p
              JOIN "SIGERSA"."CASO" AS c ON c.id = p.caso_id
              JOIN "SIGERSA"."EMPRESA" AS e ON e.id = c.empresa_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" AS est ON est.id = c.establecimiento_id
              LEFT JOIN "SIGERSA"."SOLICITUD" AS request ON request.id = c.solicitud_id
              LEFT JOIN "SIGERSA"."MUNICIPIO" AS municipality ON municipality.id = est.municipio_id
              LEFT JOIN "SIGERSA"."ASIGNACION" AS a ON a.programacion_id = p.id AND a.estado = 'ACTIVA'
              LEFT JOIN "SIGERSA"."USUARIO" AS u ON u.id = a.evaluador_id
             WHERE (@Status IS NULL OR p.estado = @Status)
               AND (@AssignedOnly = false OR EXISTS (
                   SELECT 1 FROM "SIGERSA"."ASIGNACION" assigned
                    WHERE assigned.programacion_id = p.id AND assigned.evaluador_id = @ActorId
                      AND assigned.estado = 'ACTIVA'))
               AND (@Search IS NULL OR lower(c.numero) LIKE '%' || @Search || '%'
                    OR lower(e.razon_social) LIKE '%' || @Search || '%'
                    OR lower(est.nombre) LIKE '%' || @Search || '%')
             GROUP BY p.id, c.numero, c.origen, request.numero, e.razon_social, est.nombre, est.calle, est.numero_direccion, municipality.nombre
             ORDER BY p.inicio_programado DESC, p.id
             OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*) FROM "SIGERSA"."PROGRAMACION" AS p
              JOIN "SIGERSA"."CASO" AS c ON c.id = p.caso_id
              JOIN "SIGERSA"."EMPRESA" AS e ON e.id = c.empresa_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" AS est ON est.id = c.establecimiento_id
             WHERE (@Status IS NULL OR p.estado = @Status)
               AND (@AssignedOnly = false OR EXISTS (
                   SELECT 1 FROM "SIGERSA"."ASIGNACION" assigned
                    WHERE assigned.programacion_id = p.id AND assigned.evaluador_id = @ActorId
                      AND assigned.estado = 'ACTIVA'))
               AND (@Search IS NULL OR lower(c.numero) LIKE '%' || @Search || '%'
                    OR lower(e.razon_social) LIKE '%' || @Search || '%'
                    OR lower(est.nombre) LIKE '%' || @Search || '%');
            """;
        var parameters = new { query.Search, query.Status, query.ActorId, query.AssignedOnly,
            Offset = (query.Page - 1) * query.PageSize, query.PageSize };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(Sql(sql), parameters, cancellationToken: cancellationToken));
            var items = (await result.ReadAsync<ScheduleRow>()).Select(row => row.ToDomain()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new SchedulesPage(items, query.Page, query.PageSize, total);
        }
    }

    public async Task<ScheduleOptions> GetOptionsAsync(bool canManage, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT c.id AS Id,
                   c.numero || ' · ' || replace(c.origen, '_', ' ') ||
                   COALESCE(' · Solicitud ' || request.numero, '') ||
                   ' · Prioridad ' || c.prioridad::text || ' · ' || e.razon_social || ' · ' || est.nombre AS Name
              FROM "SIGERSA"."CASO" AS c
              JOIN "SIGERSA"."EMPRESA" AS e ON e.id = c.empresa_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" AS est ON est.id = c.establecimiento_id
              LEFT JOIN "SIGERSA"."SOLICITUD" AS request ON request.id = c.solicitud_id
             WHERE c.estado <> 'CERRADO' AND @CanManage = true
             ORDER BY c.abierto_en DESC;

            SELECT DISTINCT u.id AS Id, u.nombre_completo AS Name
              FROM "SIGERSA"."USUARIO" AS u
              JOIN "SIGERSA"."USUARIO_ROL" AS ur ON ur.usuario_id = u.id AND ur.activo = true
              JOIN "SIGERSA"."ROL" AS r ON r.id = ur.rol_id
             WHERE u.activo = true AND u.estado = 'ACTIVO' AND r.codigo = 'TECNICO_EVALUADOR'
               AND @CanManage = true
             ORDER BY u.nombre_completo;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(Sql(sql), new { CanManage = canManage }, cancellationToken: cancellationToken));
            var cases = (await result.ReadAsync<OptionRow>()).Select(row => new ScheduleOption(row.Id, row.Name)).ToArray();
            var evaluators = (await result.ReadAsync<OptionRow>()).Select(row => new ScheduleOption(row.Id, row.Name)).ToArray();
            return new ScheduleOptions(cases, evaluators, canManage);
        }
    }

    public async Task<Guid> CreateAsync(ScheduleDraft draft, Guid actorId, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(draft);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var existing = await ClaimAsync(connection, transaction, draft.IdempotencyKey, actorId, payload, hash, cancellationToken);
            if (existing.HasValue) { await transaction.CommitAsync(cancellationToken); return existing.Value; }
            await ValidateEvaluatorsAsync(connection, transaction, draft.EvaluatorIds, cancellationToken);
            var id = Guid.NewGuid();
            var inserted = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."PROGRAMACION"
                    (id, caso_id, inicio_programado, fin_programado, prioridad, estado,
                     observaciones, programado_por, creado_por)
                SELECT @Id, id, @StartsAt, @EndsAt, @Priority, 'PROGRAMADA',
                       @Observations, @ActorId, @ActorId
                  FROM "SIGERSA"."CASO" WHERE id = @CaseId AND estado <> 'CERRADO';
                """), new { Id = id, draft.CaseId, draft.StartsAt, draft.EndsAt, draft.Priority, draft.Observations, ActorId = actorId }, transaction, cancellationToken: cancellationToken));
            if (inserted != 1) throw new ArgumentException("El caso no existe o está cerrado.");
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."PROGRAMACION_HISTORIAL"
                    (id, programacion_id, accion, inicio_nuevo, fin_nuevo, prioridad_nueva,
                     estado_nuevo, motivo, realizado_por, creado_por)
                VALUES (gen_random_uuid(), @Id, 'CREADA', @StartsAt, @EndsAt, @Priority,
                        'PROGRAMADA', 'Creación de la programación', @ActorId, @ActorId);
                """), new { Id = id, draft.StartsAt, draft.EndsAt, draft.Priority, ActorId = actorId },
                transaction, cancellationToken: cancellationToken));
            await ReplaceAssignmentsAsync(connection, transaction, id, draft.EvaluatorIds,
                draft.ChangeReason ?? "Asignación inicial", actorId, cancellationToken);
            await CompleteOperationAsync(connection, transaction, draft.IdempotencyKey, id, actorId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return id;
        }
    }

    public async Task<bool> UpdateAsync(Guid id, ScheduleDraft draft, Guid actorId, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await ValidateEvaluatorsAsync(connection, transaction, draft.EvaluatorIds, cancellationToken);
            var previous = await connection.QuerySingleOrDefaultAsync<ScheduleHistoryRow>(new CommandDefinition(Sql("""
                SELECT inicio_programado AS StartsAt, fin_programado AS EndsAt,
                       prioridad AS Priority, estado AS Status
                  FROM "SIGERSA"."PROGRAMACION"
                 WHERE id = @Id AND version_fila = @RowVersion
                   AND estado IN ('PROGRAMADA', 'REPROGRAMADA')
                 FOR UPDATE;
                """), new { Id = id, draft.RowVersion }, transaction, cancellationToken: cancellationToken));
            if (previous is null) { await transaction.RollbackAsync(cancellationToken); return false; }
            var affected = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."PROGRAMACION"
                   SET inicio_programado = @StartsAt, fin_programado = @EndsAt,
                       prioridad = @Priority, observaciones = @Observations,
                       motivo_cambio = @ChangeReason, estado = 'REPROGRAMADA',
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE id = @Id AND version_fila = @RowVersion
                   AND estado IN ('PROGRAMADA', 'REPROGRAMADA');
                """), new { Id = id, draft.StartsAt, draft.EndsAt, draft.Priority, draft.Observations, draft.ChangeReason, draft.RowVersion, ActorId = actorId }, transaction, cancellationToken: cancellationToken));
            if (affected != 1) { await transaction.RollbackAsync(cancellationToken); return false; }
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."PROGRAMACION_HISTORIAL"
                    (id, programacion_id, accion, inicio_anterior, fin_anterior,
                     inicio_nuevo, fin_nuevo, prioridad_anterior, prioridad_nueva,
                     estado_anterior, estado_nuevo, motivo, realizado_por, creado_por)
                VALUES (gen_random_uuid(), @Id, 'REPROGRAMADA', @PreviousStartsAt, @PreviousEndsAt,
                        @StartsAt, @EndsAt, @PreviousPriority, @Priority,
                        @PreviousStatus, 'REPROGRAMADA', @ChangeReason, @ActorId, @ActorId);
                """), new { Id = id, PreviousStartsAt = previous.StartsAt, PreviousEndsAt = previous.EndsAt,
                    PreviousPriority = previous.Priority, PreviousStatus = previous.Status,
                    draft.StartsAt, draft.EndsAt, draft.Priority, draft.ChangeReason, ActorId = actorId },
                transaction, cancellationToken: cancellationToken));
            await ReplaceAssignmentsAsync(connection, transaction, id, draft.EvaluatorIds,
                draft.ChangeReason ?? "Reasignación", actorId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }

    public async Task<bool> CancelAsync(Guid id, long rowVersion, string reason, Guid actorId, CancellationToken cancellationToken = default)
    {
        const string cancelScheduleSql = """
            UPDATE "SIGERSA"."PROGRAMACION"
               SET estado = 'CANCELADA', motivo_cambio = @Reason,
                   modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                   version_fila = version_fila + 1
             WHERE id = @Id AND version_fila = @RowVersion
               AND estado IN ('PROGRAMADA', 'REPROGRAMADA');
            """;
        const string revokeAssignmentsSql = """
            UPDATE "SIGERSA"."ASIGNACION"
               SET estado = 'REVOCADA', revocado_en = CURRENT_TIMESTAMP,
                   modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                   version_fila = version_fila + 1
             WHERE programacion_id = @Id AND estado = 'ACTIVA';
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var previous = await connection.QuerySingleOrDefaultAsync<ScheduleHistoryRow>(new CommandDefinition(Sql("""
                SELECT inicio_programado AS StartsAt, fin_programado AS EndsAt,
                       prioridad AS Priority, estado AS Status
                  FROM "SIGERSA"."PROGRAMACION"
                 WHERE id = @Id AND version_fila = @RowVersion
                   AND estado IN ('PROGRAMADA', 'REPROGRAMADA')
                 FOR UPDATE;
                """), new { Id = id, RowVersion = rowVersion }, transaction, cancellationToken: cancellationToken));
            if (previous is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
            var parameters = new { Id = id, RowVersion = rowVersion, Reason = reason, ActorId = actorId };
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                Sql(cancelScheduleSql), parameters, transaction, cancellationToken: cancellationToken));
            if (affected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                Sql(revokeAssignmentsSql), parameters, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."PROGRAMACION_HISTORIAL"
                    (id, programacion_id, accion, inicio_anterior, fin_anterior,
                     prioridad_anterior, estado_anterior, estado_nuevo, motivo,
                     realizado_por, creado_por)
                VALUES (gen_random_uuid(), @Id, 'CANCELADA', @StartsAt, @EndsAt,
                        @Priority, @Status, 'CANCELADA', @Reason, @ActorId, @ActorId);
                """), new { Id = id, previous.StartsAt, previous.EndsAt, previous.Priority,
                    previous.Status, Reason = reason, ActorId = actorId }, transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }

    private static async Task<Guid?> ClaimAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction,
        Guid key, Guid actorId, string payload, string hash, CancellationToken cancellationToken)
    {
        var inserted = await connection.ExecuteAsync(new CommandDefinition(Sql("""
            INSERT INTO "SIGERSA"."OPERACION_SINCRONIZACION"
                (id, usuario_id, idempotency_key, dispositivo_id, secuencia_cliente,
                 tipo_operacion, recurso_tipo, payload, payload_hash, estado, fecha_cliente, creado_por)
            VALUES (@Id, @ActorId, @Key, @Key, 0, 'CREAR_PROGRAMACION', 'PROGRAMACION',
                    CAST(@Payload AS jsonb), @Hash, 'RECIBIDA', CURRENT_TIMESTAMP, @ActorId)
            ON CONFLICT (idempotency_key) DO NOTHING;
            """), new { Id = Guid.NewGuid(), ActorId = actorId, Key = key, Payload = payload, Hash = hash }, transaction, cancellationToken: cancellationToken));
        if (inserted == 1) return null;
        var row = await connection.QuerySingleAsync<OperationRow>(new CommandDefinition(Sql("""
            SELECT recurso_id AS ResourceId, payload_hash AS PayloadHash FROM "SIGERSA"."OPERACION_SINCRONIZACION" WHERE idempotency_key = @Key;
            """), new { Key = key }, transaction, cancellationToken: cancellationToken));
        if (!string.Equals(row.PayloadHash, hash, StringComparison.Ordinal) || !row.ResourceId.HasValue)
            throw new InvalidOperationException("La clave de idempotencia ya fue utilizada con otros datos.");
        return row.ResourceId;
    }

    private static Task<int> CompleteOperationAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction,
        Guid key, Guid resourceId, Guid actorId, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(Sql("""
            UPDATE "SIGERSA"."OPERACION_SINCRONIZACION" SET recurso_id = @ResourceId,
                estado = 'APLICADA', codigo_resultado = 'CREATED', procesada_en = CURRENT_TIMESTAMP,
                modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId, version_fila = version_fila + 1
             WHERE idempotency_key = @Key;
            """), new { Key = key, ResourceId = resourceId, ActorId = actorId }, transaction, cancellationToken: cancellationToken));

    private static async Task ValidateEvaluatorsAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction,
        Guid[] evaluatorIds, CancellationToken cancellationToken)
    {
        var count = await connection.QuerySingleAsync<int>(new CommandDefinition(Sql("""
            SELECT COUNT(DISTINCT u.id) FROM "SIGERSA"."USUARIO" AS u
              JOIN "SIGERSA"."USUARIO_ROL" AS ur ON ur.usuario_id = u.id AND ur.activo = true
              JOIN "SIGERSA"."ROL" AS r ON r.id = ur.rol_id
             WHERE u.id = ANY(@EvaluatorIds) AND u.activo = true AND u.estado = 'ACTIVO'
               AND r.codigo = 'TECNICO_EVALUADOR';
            """), new { EvaluatorIds = evaluatorIds }, transaction, cancellationToken: cancellationToken));
        if (count != evaluatorIds.Length) throw new ArgumentException("Uno o más técnicos no están activos o no tienen el rol requerido.");
    }

    private static async Task ReplaceAssignmentsAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction,
        Guid scheduleId, Guid[] evaluatorIds, string reason, Guid actorId, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(Sql("""
            UPDATE "SIGERSA"."ASIGNACION" SET estado = 'REVOCADA', revocado_en = CURRENT_TIMESTAMP,
                   modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId, version_fila = version_fila + 1
             WHERE programacion_id = @ScheduleId AND estado = 'ACTIVA';

            INSERT INTO "SIGERSA"."ASIGNACION"
                (id, programacion_id, evaluador_id, es_principal, estado, motivo_asignacion, asignado_por, creado_por)
            SELECT gen_random_uuid(), @ScheduleId, value, ordinality = 1, 'ACTIVA', @Reason, @ActorId, @ActorId
              FROM unnest(@EvaluatorIds::uuid[]) WITH ORDINALITY AS selected(value, ordinality)
            ON CONFLICT (programacion_id, evaluador_id) DO UPDATE
                SET es_principal = EXCLUDED.es_principal, estado = 'ACTIVA', revocado_en = NULL,
                    asignado_en = CURRENT_TIMESTAMP, asignado_por = @ActorId,
                    motivo_asignacion = @Reason,
                    modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                    version_fila = "SIGERSA"."ASIGNACION".version_fila + 1;
            """), new { ScheduleId = scheduleId, EvaluatorIds = evaluatorIds, Reason = reason, ActorId = actorId }, transaction, cancellationToken: cancellationToken));
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private sealed class OptionRow { public Guid Id { get; init; } public string Name { get; init; } = string.Empty; }
    private sealed class OperationRow { public Guid? ResourceId { get; init; } public string PayloadHash { get; init; } = string.Empty; }
    private sealed class ScheduleHistoryRow
    {
        public DateTime StartsAt { get; init; }
        public DateTime EndsAt { get; init; }
        public short Priority { get; init; }
        public string Status { get; init; } = string.Empty;
    }
    private sealed class ScheduleRow
    {
        public Guid Id { get; init; } public Guid CaseId { get; init; } public string CaseNumber { get; init; } = string.Empty;
        public string CompanyName { get; init; } = string.Empty; public string EstablishmentName { get; init; } = string.Empty;
        public string CaseOrigin { get; init; } = string.Empty; public string? RequestNumber { get; init; }
        public string? Location { get; init; }
        public DateTime StartsAt { get; init; } public DateTime EndsAt { get; init; } public short Priority { get; init; }
        public string Status { get; init; } = string.Empty; public string? Observations { get; init; } public string? ChangeReason { get; init; }
        public Guid[] EvaluatorIds { get; init; } = []; public string[] EvaluatorNames { get; init; } = []; public long RowVersion { get; init; }
        public ScheduleRecord ToDomain() => new(Id, CaseId, CaseNumber, CompanyName, EstablishmentName, CaseOrigin, RequestNumber, Location,
            Utc(StartsAt), Utc(EndsAt), Priority, Status, Observations, ChangeReason, EvaluatorIds, EvaluatorNames, RowVersion);
    }
}
