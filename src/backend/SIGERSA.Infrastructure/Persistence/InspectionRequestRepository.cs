using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class InspectionRequestRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IInspectionRequestRepository
{
    public async Task<InspectionRequestsPage> SearchAsync(
        InspectionRequestSearch query,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT s.id AS Id, s.numero AS Number, s.empresa_id AS CompanyId,
                   e.razon_social AS CompanyName, s.establecimiento_id AS EstablishmentId,
                   est.nombre AS EstablishmentName, s.solicitante_id AS ApplicantId,
                   u.nombre_completo AS ApplicantName, s.motivo_inspeccion_id AS InspectionReasonId,
                   m.nombre AS InspectionReasonName, s.motivo_detalle AS ReasonDetail,
                   s.tipo_establecimiento AS EstablishmentType, s.observaciones AS Observations,
                   (SELECT COUNT(*) FROM "SIGERSA"."SOLICITUD_DOCUMENTO" document
                     WHERE document.solicitud_id = s.id AND document.activo = true)::integer AS DocumentCount,
                   s.estado AS Status, s.creado_en AS CreatedAt, s.enviada_en AS SubmittedAt,
                   s.cancelada_en AS CancelledAt, s.version_fila AS RowVersion
              FROM "SIGERSA"."SOLICITUD" AS s
              JOIN "SIGERSA"."EMPRESA" AS e ON e.id = s.empresa_id
              JOIN "SIGERSA"."USUARIO" AS u ON u.id = s.solicitante_id
              JOIN "SIGERSA"."MOTIVO_INSPECCION" AS m ON m.id = s.motivo_inspeccion_id
              LEFT JOIN "SIGERSA"."ESTABLECIMIENTO" AS est ON est.id = s.establecimiento_id
             WHERE s.activo = true
               AND (
                    @GlobalScope = true
                    OR (@CompanyScope IS NOT NULL AND s.empresa_id = @CompanyScope)
                    OR (@AssignedOnly = true AND EXISTS (
                        SELECT 1
                          FROM "SIGERSA"."CASO" AS c
                          JOIN "SIGERSA"."EVALUACION" AS ev ON ev.caso_id = c.id
                         WHERE c.solicitud_id = s.id
                           AND ev.evaluador_principal_id = @ActorId))
               )
               AND (@Status IS NULL OR s.estado = @Status)
               AND (@Search IS NULL OR lower(COALESCE(s.numero, '')) LIKE '%' || @Search || '%'
                    OR lower(e.razon_social) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(est.nombre, '')) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(s.motivo_detalle, '')) LIKE '%' || @Search || '%')
             ORDER BY s.creado_en DESC, s.id
             OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
              FROM "SIGERSA"."SOLICITUD" AS s
              JOIN "SIGERSA"."EMPRESA" AS e ON e.id = s.empresa_id
              LEFT JOIN "SIGERSA"."ESTABLECIMIENTO" AS est ON est.id = s.establecimiento_id
             WHERE s.activo = true
               AND (
                    @GlobalScope = true
                    OR (@CompanyScope IS NOT NULL AND s.empresa_id = @CompanyScope)
                    OR (@AssignedOnly = true AND EXISTS (
                        SELECT 1
                          FROM "SIGERSA"."CASO" AS c
                          JOIN "SIGERSA"."EVALUACION" AS ev ON ev.caso_id = c.id
                         WHERE c.solicitud_id = s.id
                           AND ev.evaluador_principal_id = @ActorId))
               )
               AND (@Status IS NULL OR s.estado = @Status)
               AND (@Search IS NULL OR lower(COALESCE(s.numero, '')) LIKE '%' || @Search || '%'
                    OR lower(e.razon_social) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(est.nombre, '')) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(s.motivo_detalle, '')) LIKE '%' || @Search || '%');
            """;
        var parameters = new
        {
            query.Search,
            query.Status,
            query.ActorId,
            query.CompanyScope,
            query.GlobalScope,
            query.AssignedOnly,
            Offset = (query.Page - 1) * query.PageSize,
            query.PageSize
        };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(
                new CommandDefinition(Sql(sql), parameters, cancellationToken: cancellationToken));
            var rows = (await result.ReadAsync<RequestRow>()).Select(row => row.ToDomain()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new InspectionRequestsPage(rows, query.Page, query.PageSize, total);
        }
    }

    public async Task<InspectionRequestOptions> GetOptionsAsync(
        Guid? companyScope,
        bool canManage,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, razon_social AS Name, NULL::uuid AS CompanyId
              FROM "SIGERSA"."EMPRESA"
             WHERE activo = true AND estado = 'ACTIVA' AND @CanManage = true
               AND (@CompanyScope IS NULL OR id = @CompanyScope)
             ORDER BY razon_social;

            SELECT id AS Id, nombre AS Name, empresa_id AS CompanyId
              FROM "SIGERSA"."ESTABLECIMIENTO"
             WHERE activo = true AND estado = 'ACTIVO' AND @CanManage = true
               AND (@CompanyScope IS NULL OR empresa_id = @CompanyScope)
             ORDER BY nombre;

            SELECT id AS Id, nombre AS Name, NULL::uuid AS CompanyId
              FROM "SIGERSA"."MOTIVO_INSPECCION"
             WHERE activo = true AND @CanManage = true
             ORDER BY orden, nombre;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql),
                new { CompanyScope = companyScope, CanManage = canManage },
                cancellationToken: cancellationToken));
            var companies = (await result.ReadAsync<OptionRow>()).Select(ToOption).ToArray();
            var establishments = (await result.ReadAsync<OptionRow>()).Select(ToOption).ToArray();
            var reasons = (await result.ReadAsync<OptionRow>()).Select(ToOption).ToArray();
            return new InspectionRequestOptions(companies, establishments, reasons, canManage);
        }
    }

    public async Task<Guid> CreateAsync(
        InspectionRequestDraft draft,
        Guid applicantId,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await ValidateReferencesAsync(connection, transaction, draft, cancellationToken);
            var id = Guid.NewGuid();
            var insertedId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."SOLICITUD"
                    (id, idempotency_key, empresa_id, establecimiento_id, solicitante_id,
                     motivo_inspeccion_id, motivo_detalle, tipo_establecimiento, observaciones,
                     estado, creado_por)
                VALUES
                    (@Id, @IdempotencyKey, @CompanyId, @EstablishmentId, @ApplicantId,
                     @InspectionReasonId, @ReasonDetail, @EstablishmentType, @Observations,
                     'BORRADOR', @ApplicantId)
                ON CONFLICT (idempotency_key) DO NOTHING
                RETURNING id;
                """), new
            {
                Id = id,
                draft.IdempotencyKey,
                draft.CompanyId,
                draft.EstablishmentId,
                ApplicantId = applicantId,
                draft.InspectionReasonId,
                draft.ReasonDetail,
                draft.EstablishmentType,
                draft.Observations
            }, transaction, cancellationToken: cancellationToken));

            if (insertedId.HasValue)
            {
                await transaction.CommitAsync(cancellationToken);
                return insertedId.Value;
            }

            var existing = await connection.QuerySingleAsync<IdempotentRequestRow>(new CommandDefinition(Sql("""
                SELECT id AS Id, empresa_id AS CompanyId, establecimiento_id AS EstablishmentId,
                       solicitante_id AS ApplicantId, motivo_inspeccion_id AS InspectionReasonId,
                       motivo_detalle AS ReasonDetail, tipo_establecimiento AS EstablishmentType,
                       observaciones AS Observations
                  FROM "SIGERSA"."SOLICITUD"
                 WHERE idempotency_key = @IdempotencyKey;
                """), new { draft.IdempotencyKey }, transaction, cancellationToken: cancellationToken));
            if (!existing.Matches(draft, applicantId))
                throw new InvalidOperationException("La clave de idempotencia ya fue utilizada con otros datos.");
            await transaction.CommitAsync(cancellationToken);
            return existing.Id;
        }
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        InspectionRequestDraft draft,
        Guid actorId,
        Guid? companyScope,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await ValidateReferencesAsync(connection, transaction, draft, cancellationToken);
            var affected = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."SOLICITUD"
                   SET empresa_id = @CompanyId, establecimiento_id = @EstablishmentId,
                       motivo_inspeccion_id = @InspectionReasonId, motivo_detalle = @ReasonDetail,
                       tipo_establecimiento = @EstablishmentType, observaciones = @Observations,
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE id = @Id AND activo = true AND estado = 'BORRADOR'
                   AND version_fila = @RowVersion
                   AND (@CompanyScope IS NULL OR empresa_id = @CompanyScope);
                """), new
            {
                Id = id,
                draft.CompanyId,
                draft.EstablishmentId,
                draft.InspectionReasonId,
                draft.ReasonDetail,
                draft.EstablishmentType,
                draft.Observations,
                RowVersion = draft.RowVersion,
                ActorId = actorId,
                CompanyScope = companyScope
            }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return affected == 1;
        }
    }

    public async Task<bool> TransitionAsync(
        Guid id,
        long rowVersion,
        string targetStatus,
        Guid actorId,
        Guid? companyScope,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."SOLICITUD"
               SET estado = @TargetStatus,
                   numero = CASE WHEN @TargetStatus = 'PENDIENTE_ASIGNACION' THEN COALESCE(
                       numero,
                       'SOL-' || to_char(CURRENT_TIMESTAMP, 'YYYYMMDD') || '-' ||
                       upper(substr(replace(id::text, '-', ''), 1, 8))) ELSE numero END,
                   enviada_en = CASE WHEN @TargetStatus = 'PENDIENTE_ASIGNACION' THEN CURRENT_TIMESTAMP ELSE enviada_en END,
                   cancelada_en = CASE WHEN @TargetStatus = 'CANCELADA' THEN CURRENT_TIMESTAMP ELSE cancelada_en END,
                   modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                   version_fila = version_fila + 1
             WHERE id = @Id AND activo = true AND version_fila = @RowVersion
               AND (@CompanyScope IS NULL OR empresa_id = @CompanyScope)
               AND ((@TargetStatus = 'PENDIENTE_ASIGNACION' AND estado = 'BORRADOR')
                    OR (@TargetStatus = 'CANCELADA' AND estado IN ('BORRADOR', 'PENDIENTE_ASIGNACION')))
               AND (@TargetStatus <> 'PENDIENTE_ASIGNACION' OR EXISTS (
                   SELECT 1 FROM "SIGERSA"."SOLICITUD_DOCUMENTO" document
                    WHERE document.solicitud_id = id AND document.activo = true
                      AND document.obligatorio = true));
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                Sql(sql),
                new
                {
                    Id = id,
                    RowVersion = rowVersion,
                    TargetStatus = targetStatus,
                    ActorId = actorId,
                    CompanyScope = companyScope
                },
                cancellationToken: cancellationToken));
            return affected == 1;
        }
    }

    public async Task<bool> HasRequiredDocumentAsync(
        Guid id,
        Guid? companyScope,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM "SIGERSA"."SOLICITUD" request
                 WHERE request.id = @Id AND request.activo = true AND request.estado = 'BORRADOR'
                   AND (@CompanyScope IS NULL OR request.empresa_id = @CompanyScope)
                   AND EXISTS (SELECT 1 FROM "SIGERSA"."SOLICITUD_DOCUMENTO" document
                        WHERE document.solicitud_id = request.id AND document.activo = true
                          AND document.obligatorio = true));
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
            return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                Sql(sql), new { Id = id, CompanyScope = companyScope }, cancellationToken: cancellationToken));
    }

    private static async Task ValidateReferencesAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        InspectionRequestDraft draft,
        CancellationToken cancellationToken)
    {
        var valid = await connection.QuerySingleAsync<bool>(new CommandDefinition(Sql("""
            SELECT EXISTS (
                SELECT 1 FROM "SIGERSA"."MOTIVO_INSPECCION"
                 WHERE id = @InspectionReasonId AND activo = true)
              AND (@EstablishmentId IS NULL OR EXISTS (
                SELECT 1 FROM "SIGERSA"."ESTABLECIMIENTO"
                 WHERE id = @EstablishmentId AND empresa_id = @CompanyId AND activo = true))
              AND EXISTS (
                SELECT 1 FROM "SIGERSA"."EMPRESA"
                 WHERE id = @CompanyId AND activo = true);
            """), new
        {
            draft.InspectionReasonId,
            draft.EstablishmentId,
            draft.CompanyId
        }, transaction, cancellationToken: cancellationToken));
        if (!valid) throw new ArgumentException("La empresa, el establecimiento o el motivo no es válido para la solicitud.");
    }

    private static InspectionRequestOption ToOption(OptionRow row) =>
        new(row.Id, row.Name, row.CompanyId);

    private sealed class OptionRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public Guid? CompanyId { get; init; }
    }

    private sealed class RequestRow
    {
        public Guid Id { get; init; }
        public string? Number { get; init; }
        public Guid CompanyId { get; init; }
        public string CompanyName { get; init; } = string.Empty;
        public Guid? EstablishmentId { get; init; }
        public string? EstablishmentName { get; init; }
        public Guid ApplicantId { get; init; }
        public string ApplicantName { get; init; } = string.Empty;
        public Guid InspectionReasonId { get; init; }
        public string InspectionReasonName { get; init; } = string.Empty;
        public string? ReasonDetail { get; init; }
        public string? EstablishmentType { get; init; }
        public string? Observations { get; init; }
        public int DocumentCount { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime? SubmittedAt { get; init; }
        public DateTime? CancelledAt { get; init; }
        public long RowVersion { get; init; }

        public InspectionRequestRecord ToDomain() => new(
            Id, Number, CompanyId, CompanyName, EstablishmentId, EstablishmentName,
            ApplicantId, ApplicantName, InspectionReasonId, InspectionReasonName,
            ReasonDetail, EstablishmentType, Observations, DocumentCount, Status, Utc(CreatedAt),
            SubmittedAt.HasValue ? Utc(SubmittedAt.Value) : null,
            CancelledAt.HasValue ? Utc(CancelledAt.Value) : null, RowVersion);

        private static DateTimeOffset Utc(DateTime value) =>
            new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private sealed class IdempotentRequestRow
    {
        public Guid Id { get; init; }
        public Guid CompanyId { get; init; }
        public Guid? EstablishmentId { get; init; }
        public Guid ApplicantId { get; init; }
        public Guid InspectionReasonId { get; init; }
        public string? ReasonDetail { get; init; }
        public string? EstablishmentType { get; init; }
        public string? Observations { get; init; }

        public bool Matches(InspectionRequestDraft draft, Guid applicantId) =>
            CompanyId == draft.CompanyId &&
            EstablishmentId == draft.EstablishmentId &&
            ApplicantId == applicantId &&
            InspectionReasonId == draft.InspectionReasonId &&
            string.Equals(ReasonDetail, draft.ReasonDetail, StringComparison.Ordinal) &&
            string.Equals(EstablishmentType, draft.EstablishmentType, StringComparison.Ordinal) &&
            string.Equals(Observations, draft.Observations, StringComparison.Ordinal);
    }
}
