using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class CaseRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), ICaseRepository
{
    public async Task<CasesPage> SearchAsync(CaseSearch query, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT c.id AS Id, c.numero AS Number, c.solicitud_id AS RequestId,
                   c.alerta_lapch_id AS AlertId, c.denuncia_id AS ComplaintId,
                   c.empresa_id AS CompanyId, e.razon_social AS CompanyName,
                   c.establecimiento_id AS EstablishmentId, est.nombre AS EstablishmentName,
                   c.origen AS Origin, c.estado AS Status, c.prioridad AS Priority,
                   c.responsable_actual_id AS ResponsibleId, u.nombre_completo AS ResponsibleName,
                   c.decision_analisis AS AnalysisDecision, c.motivo_decision AS DecisionReason,
                   c.abierto_en AS OpenedAt, c.cerrado_en AS ClosedAt, c.version_fila AS RowVersion
              FROM "SIGERSA"."CASO" AS c
              JOIN "SIGERSA"."EMPRESA" AS e ON e.id = c.empresa_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" AS est ON est.id = c.establecimiento_id
              LEFT JOIN "SIGERSA"."USUARIO" AS u ON u.id = c.responsable_actual_id
             WHERE (@GlobalScope = true
                    OR (@CompanyScope IS NOT NULL AND c.empresa_id = @CompanyScope AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."SOLICITUD" owner_request
                         WHERE owner_request.id = c.solicitud_id AND owner_request.solicitante_id = @ActorId))
                    OR (@AssignedOnly = true AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."EVALUACION" AS ev
                         WHERE ev.caso_id = c.id AND ev.evaluador_principal_id = @ActorId)))
               AND (@Status IS NULL OR c.estado = @Status)
               AND (@Search IS NULL OR lower(c.numero) LIKE '%' || @Search || '%'
                    OR lower(e.razon_social) LIKE '%' || @Search || '%'
                    OR lower(est.nombre) LIKE '%' || @Search || '%')
             ORDER BY c.abierto_en DESC, c.id
             OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
              FROM "SIGERSA"."CASO" AS c
              JOIN "SIGERSA"."EMPRESA" AS e ON e.id = c.empresa_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" AS est ON est.id = c.establecimiento_id
             WHERE (@GlobalScope = true
                    OR (@CompanyScope IS NOT NULL AND c.empresa_id = @CompanyScope AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."SOLICITUD" owner_request
                         WHERE owner_request.id = c.solicitud_id AND owner_request.solicitante_id = @ActorId))
                    OR (@AssignedOnly = true AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."EVALUACION" AS ev
                         WHERE ev.caso_id = c.id AND ev.evaluador_principal_id = @ActorId)))
               AND (@Status IS NULL OR c.estado = @Status)
               AND (@Search IS NULL OR lower(c.numero) LIKE '%' || @Search || '%'
                    OR lower(e.razon_social) LIKE '%' || @Search || '%'
                    OR lower(est.nombre) LIKE '%' || @Search || '%');
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
            using var result = await connection.QueryMultipleAsync(
                new CommandDefinition(Sql(sql), parameters, cancellationToken: cancellationToken));
            var items = (await result.ReadAsync<CaseRow>()).Select(row => row.ToDomain()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new CasesPage(items, query.Page, query.PageSize, total);
        }
    }

    public async Task<CaseOptions> GetOptionsAsync(bool canManage, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT source.id AS Id, source.kind AS Kind, source.name AS Name,
                   source.company_id AS CompanyId, source.establishment_id AS EstablishmentId
              FROM (
                    SELECT s.id, 'SOLICITUD_EMPRESA'::varchar AS kind,
                           'Solicitud · ' || COALESCE(s.numero, 'Sin número') || ' · ' || e.razon_social || ' · ' || est.nombre AS name,
                           s.empresa_id AS company_id, s.establecimiento_id AS establishment_id, s.enviada_en AS occurred_at
                      FROM "SIGERSA"."SOLICITUD" s
                      JOIN "SIGERSA"."EMPRESA" e ON e.id = s.empresa_id
                      JOIN "SIGERSA"."ESTABLECIMIENTO" est ON est.id = s.establecimiento_id
                     WHERE s.estado = 'PENDIENTE_ASIGNACION' AND s.activo = true
                       AND NOT EXISTS (SELECT 1 FROM "SIGERSA"."CASO" c WHERE c.solicitud_id = s.id)
                    UNION ALL
                    SELECT alert.id, 'ALERTA_LAPCH',
                           'Alerta LAPCH · ' || alert.numero_alerta || ' · ' || company.razon_social || ' · ' || establishment.nombre,
                           COALESCE(alert.empresa_id, establishment.empresa_id), establishment.id, alert.fecha_alerta
                      FROM "SIGERSA"."ALERTA_LAPCH" alert
                      JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = alert.establecimiento_id
                      JOIN "SIGERSA"."EMPRESA" company ON company.id = COALESCE(alert.empresa_id, establishment.empresa_id)
                     WHERE alert.resultado = 'PROCEDE_EVALUACION'
                       AND NOT EXISTS (SELECT 1 FROM "SIGERSA"."CASO" c WHERE c.alerta_lapch_id = alert.id)
                    UNION ALL
                    SELECT complaint.id, 'DENUNCIA',
                           'Denuncia · ' || complaint.numero || ' · ' || company.razon_social || ' · ' || establishment.nombre,
                           establishment.empresa_id, establishment.id, complaint.fecha_recepcion
                      FROM "SIGERSA"."DENUNCIA" complaint
                      JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = complaint.establecimiento_id
                      JOIN "SIGERSA"."EMPRESA" company ON company.id = establishment.empresa_id
                     WHERE complaint.resultado = 'PROCEDE'
                       AND NOT EXISTS (SELECT 1 FROM "SIGERSA"."CASO" c WHERE c.denuncia_id = complaint.id)
                    UNION ALL
                    SELECT establishment.id, 'PROGRAMACION',
                           'Programa institucional · ' || company.razon_social || ' · ' || establishment.nombre,
                           establishment.empresa_id, establishment.id, CURRENT_TIMESTAMP
                      FROM "SIGERSA"."ESTABLECIMIENTO" establishment
                      JOIN "SIGERSA"."EMPRESA" company ON company.id = establishment.empresa_id
                     WHERE establishment.activo = true AND company.activo = true
                   ) source
             WHERE @CanManage = true
             ORDER BY source.occurred_at DESC, source.name;

            SELECT DISTINCT u.id AS Id, u.nombre_completo AS Name, u.empresa_id AS CompanyId
              FROM "SIGERSA"."USUARIO" AS u
              JOIN "SIGERSA"."USUARIO_ROL" AS ur ON ur.usuario_id = u.id AND ur.activo = true
              JOIN "SIGERSA"."ROL" AS r ON r.id = ur.rol_id
             WHERE u.activo = true AND u.estado = 'ACTIVO' AND @CanManage = true
               AND r.codigo = 'COORDINADOR'
             ORDER BY u.nombre_completo;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), new { CanManage = canManage }, cancellationToken: cancellationToken));
            var sources = (await result.ReadAsync<SourceOptionRow>()).Select(row => new CaseSourceOption(
                row.Id, row.Kind, row.Name, row.CompanyId, row.EstablishmentId)).ToArray();
            var users = (await result.ReadAsync<OptionRow>()).Select(ToOption).ToArray();
            return new CaseOptions(sources, users, canManage);
        }
    }

    public async Task<Guid> CreateAsync(CaseDraft draft, Guid actorId, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(draft);
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var claimed = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."OPERACION_SINCRONIZACION"
                    (id, usuario_id, idempotency_key, dispositivo_id, secuencia_cliente,
                     tipo_operacion, recurso_tipo, payload, payload_hash, estado, fecha_cliente, creado_por)
                VALUES
                    (@OperationId, @ActorId, @IdempotencyKey, @IdempotencyKey, 0,
                     'CREAR_CASO', 'CASO', CAST(@Payload AS jsonb), @PayloadHash,
                     'RECIBIDA', CURRENT_TIMESTAMP, @ActorId)
                ON CONFLICT (idempotency_key) DO NOTHING;
                """), new
            {
                OperationId = Guid.NewGuid(), ActorId = actorId, draft.IdempotencyKey, Payload = payload, PayloadHash = payloadHash
            }, transaction, cancellationToken: cancellationToken));
            if (claimed == 0)
            {
                var existing = await connection.QuerySingleAsync<OperationRow>(new CommandDefinition(Sql("""
                    SELECT recurso_id AS ResourceId, payload_hash AS PayloadHash
                      FROM "SIGERSA"."OPERACION_SINCRONIZACION"
                     WHERE idempotency_key = @IdempotencyKey;
                    """), new { draft.IdempotencyKey }, transaction, cancellationToken: cancellationToken));
                if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal) || !existing.ResourceId.HasValue)
                    throw new InvalidOperationException("La clave de idempotencia ya fue utilizada con otros datos.");
                await transaction.CommitAsync(cancellationToken);
                return existing.ResourceId.Value;
            }

            var id = Guid.NewGuid();
            var inserted = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                WITH source AS (
                    SELECT s.id, s.empresa_id, s.establecimiento_id, s.motivo_inspeccion_id
                      FROM "SIGERSA"."SOLICITUD" s
                     WHERE @Origin = 'SOLICITUD_EMPRESA' AND s.id = @SourceId
                       AND s.estado = 'PENDIENTE_ASIGNACION' AND s.activo = true AND s.establecimiento_id IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM "SIGERSA"."CASO" c WHERE c.solicitud_id = s.id)
                    UNION ALL
                    SELECT alert.id, COALESCE(alert.empresa_id, establishment.empresa_id), establishment.id, NULL::uuid
                      FROM "SIGERSA"."ALERTA_LAPCH" alert
                      JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = alert.establecimiento_id
                     WHERE @Origin = 'ALERTA_LAPCH' AND alert.id = @SourceId
                       AND alert.resultado = 'PROCEDE_EVALUACION'
                       AND NOT EXISTS (SELECT 1 FROM "SIGERSA"."CASO" c WHERE c.alerta_lapch_id = alert.id)
                    UNION ALL
                    SELECT complaint.id, establishment.empresa_id, establishment.id, NULL::uuid
                      FROM "SIGERSA"."DENUNCIA" complaint
                      JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = complaint.establecimiento_id
                     WHERE @Origin = 'DENUNCIA' AND complaint.id = @SourceId
                       AND complaint.resultado = 'PROCEDE'
                       AND NOT EXISTS (SELECT 1 FROM "SIGERSA"."CASO" c WHERE c.denuncia_id = complaint.id)
                    UNION ALL
                    SELECT establishment.id, establishment.empresa_id, establishment.id, NULL::uuid
                      FROM "SIGERSA"."ESTABLECIMIENTO" establishment
                     WHERE @Origin = 'PROGRAMACION' AND establishment.id = @SourceId AND establishment.activo = true
                )
                INSERT INTO "SIGERSA"."CASO"
                    (id, numero, solicitud_id, empresa_id, establecimiento_id, motivo_inspeccion_id,
                     alerta_lapch_id, denuncia_id, origen, estado, prioridad, responsable_actual_id, decision_analisis,
                     motivo_decision, creado_por)
                SELECT @Id,
                       'CAS-' || to_char(CURRENT_TIMESTAMP, 'YYYYMMDD') || '-' ||
                           upper(substr(replace(@Id::text, '-', ''), 1, 8)),
                       CASE WHEN @Origin = 'SOLICITUD_EMPRESA' THEN source.id END,
                       source.empresa_id, source.establecimiento_id, source.motivo_inspeccion_id,
                       CASE WHEN @Origin = 'ALERTA_LAPCH' THEN source.id END,
                       CASE WHEN @Origin = 'DENUNCIA' THEN source.id END,
                       @Origin, 'ABIERTO', @Priority, @ResponsibleId,
                       @AnalysisDecision, @DecisionReason, @ActorId
                  FROM source;
                """), new
            {
                Id = id, draft.Origin, draft.SourceId, draft.Priority, draft.ResponsibleId,
                draft.AnalysisDecision, draft.DecisionReason, ActorId = actorId
            }, transaction, cancellationToken: cancellationToken));
            if (inserted != 1) throw new ArgumentException("El origen seleccionado no está listo para crear un caso o ya fue utilizado.");
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."SOLICITUD"
                   SET estado = 'ASIGNADA', modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId, version_fila = version_fila + 1
                 WHERE id = @SourceId AND @Origin = 'SOLICITUD_EMPRESA'
                   AND estado = 'PENDIENTE_ASIGNACION';
                """), new { draft.SourceId, draft.Origin, ActorId = actorId }, transaction,
                cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."OPERACION_SINCRONIZACION"
                   SET recurso_id = @Id, estado = 'APLICADA', codigo_resultado = 'CREATED',
                       procesada_en = CURRENT_TIMESTAMP, modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId, version_fila = version_fila + 1
                 WHERE idempotency_key = @IdempotencyKey;
                """), new { Id = id, draft.IdempotencyKey, ActorId = actorId }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return id;
        }
    }

    public async Task<bool> UpdateAsync(Guid id, CaseDraft draft, Guid actorId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."CASO"
               SET prioridad = @Priority, responsable_actual_id = @ResponsibleId,
                   decision_analisis = @AnalysisDecision, motivo_decision = @DecisionReason,
                   estado = CASE WHEN @AnalysisDecision IS NOT NULL THEN 'ANALIZADO' ELSE estado END,
                   modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                   version_fila = version_fila + 1
             WHERE id = @Id AND version_fila = @RowVersion AND estado <> 'CERRADO';
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(Sql(sql), new
            {
                Id = id, draft.Priority, draft.ResponsibleId, draft.AnalysisDecision,
                draft.DecisionReason, draft.RowVersion, ActorId = actorId
            }, cancellationToken: cancellationToken));
            return affected == 1;
        }
    }

    public async Task<bool> CloseAsync(
        Guid id, long rowVersion, string reason, Guid actorId, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var previous = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(Sql("""
                WITH target AS (
                    SELECT estado
                      FROM "SIGERSA"."CASO"
                     WHERE id = @Id AND version_fila = @RowVersion AND estado <> 'CERRADO'
                     FOR UPDATE
                ), updated AS (
                    UPDATE "SIGERSA"."CASO"
                       SET estado = 'CERRADO', cerrado_en = CURRENT_TIMESTAMP,
                           modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                           version_fila = version_fila + 1
                     WHERE id = @Id AND EXISTS (SELECT 1 FROM target)
                    RETURNING 1
                )
                SELECT target.estado FROM target JOIN updated ON true;
                """), new { Id = id, RowVersion = rowVersion, ActorId = actorId }, transaction, cancellationToken: cancellationToken));
            if (previous is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."CASO_TRANSICION"
                    (id, caso_id, estado_anterior, estado_nuevo, motivo, ejecutado_por, creado_por)
                VALUES (@TransitionId, @Id, @Previous, 'CERRADO', @Reason, @ActorId, @ActorId);
                """), new
            {
                TransitionId = Guid.NewGuid(), Id = id, Previous = previous,
                Reason = reason, ActorId = actorId
            }, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."PROGRAMACION"
                   SET estado = 'COMPLETADA', modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId, version_fila = version_fila + 1
                 WHERE caso_id = @Id AND estado IN ('PROGRAMADA', 'REPROGRAMADA');

                UPDATE "SIGERSA"."SOLICITUD" request
                   SET estado = 'RESUELTA', modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId, version_fila = request.version_fila + 1
                  FROM "SIGERSA"."CASO" inspection_case
                 WHERE inspection_case.id = @Id AND request.id = inspection_case.solicitud_id
                   AND request.estado NOT IN ('CANCELADA', 'RECHAZADA', 'RESUELTA');
                """), new { Id = id, ActorId = actorId }, transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }

    private static CaseOption ToOption(OptionRow row) => new(row.Id, row.Name, row.CompanyId);
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private sealed class OptionRow { public Guid Id { get; init; } public string Name { get; init; } = string.Empty; public Guid? CompanyId { get; init; } }
    private sealed class SourceOptionRow { public Guid Id { get; init; } public string Kind { get; init; } = string.Empty; public string Name { get; init; } = string.Empty; public Guid CompanyId { get; init; } public Guid EstablishmentId { get; init; } }
    private sealed class OperationRow { public Guid? ResourceId { get; init; } public string PayloadHash { get; init; } = string.Empty; }
    private sealed class CaseRow
    {
        public Guid Id { get; init; } public string Number { get; init; } = string.Empty; public Guid? RequestId { get; init; }
        public Guid? AlertId { get; init; } public Guid? ComplaintId { get; init; }
        public Guid CompanyId { get; init; } public string CompanyName { get; init; } = string.Empty;
        public Guid EstablishmentId { get; init; } public string EstablishmentName { get; init; } = string.Empty;
        public string Origin { get; init; } = string.Empty; public string Status { get; init; } = string.Empty; public short Priority { get; init; }
        public Guid? ResponsibleId { get; init; } public string? ResponsibleName { get; init; } public string? AnalysisDecision { get; init; }
        public string? DecisionReason { get; init; } public DateTime OpenedAt { get; init; } public DateTime? ClosedAt { get; init; } public long RowVersion { get; init; }
        public CaseRecord ToDomain() => new(Id, Number, RequestId, AlertId, ComplaintId, CompanyId, CompanyName, EstablishmentId, EstablishmentName,
            Origin, Status, Priority, ResponsibleId, ResponsibleName, AnalysisDecision, DecisionReason,
            Utc(OpenedAt), ClosedAt.HasValue ? Utc(ClosedAt.Value) : null, RowVersion);
    }
}
