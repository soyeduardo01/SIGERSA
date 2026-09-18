using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class EvidenceRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IEvidenceRepository
{
    public async Task<EvidencesPage> SearchAsync(EvidenceSearch query, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT evidence.id AS Id, evidence.evaluacion_id AS EvaluationId,
                   evaluation.numero AS EvaluationNumber, establishment.nombre AS EstablishmentName,
                   evidence.subida_por AS UploadedBy, uploader.nombre_completo AS UploadedByName,
                   evidence.nombre_original AS OriginalName, evidence.file_size AS FileSize,
                   evidence.mime_type AS MimeType, evidence.tipo_evidencia AS EvidenceType,
                   evidence.estado_sincronizacion AS SynchronizationStatus,
                   evidence.fecha_servidor AS UploadedAt,
                   (SELECT item.source_allitems_item
                      FROM "SIGERSA"."EVIDENCIA_RESPUESTA" evidence_response
                      JOIN "SIGERSA"."RESPUESTA_USUARIO" response
                        ON response.id = evidence_response.respuesta_usuario_id
                      JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
                     WHERE evidence_response.evidencia_id = evidence.id
                     ORDER BY item.orden
                     LIMIT 1) AS SourceItem,
                   (SELECT item.codigo
                      FROM "SIGERSA"."EVIDENCIA_RESPUESTA" evidence_response
                      JOIN "SIGERSA"."RESPUESTA_USUARIO" response
                        ON response.id = evidence_response.respuesta_usuario_id
                      JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
                     WHERE evidence_response.evidencia_id = evidence.id
                     ORDER BY item.orden
                     LIMIT 1) AS ItemCode,
                   (SELECT item.titulo
                      FROM "SIGERSA"."EVIDENCIA_RESPUESTA" evidence_response
                      JOIN "SIGERSA"."RESPUESTA_USUARIO" response
                        ON response.id = evidence_response.respuesta_usuario_id
                      JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
                     WHERE evidence_response.evidencia_id = evidence.id
                     ORDER BY item.orden
                     LIMIT 1) AS ItemTitle
              FROM "SIGERSA"."EVIDENCIA" evidence
              JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.id = evidence.evaluacion_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
              JOIN "SIGERSA"."USUARIO" uploader ON uploader.id = evidence.subida_por
             WHERE evidence.eliminada = false
               AND (@EvaluationId IS NULL OR evidence.evaluacion_id = @EvaluationId)
               AND (@EvidenceType IS NULL OR evidence.tipo_evidencia = @EvidenceType)
               AND (@Search IS NULL OR lower(evidence.nombre_original) LIKE '%' || @Search || '%'
                    OR lower(evaluation.numero) LIKE '%' || @Search || '%'
                    OR lower(establishment.nombre) LIKE '%' || @Search || '%')
               AND (@GlobalScope = true
                    OR (@CompanyScope IS NOT NULL AND establishment.empresa_id = @CompanyScope AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."CASO" owner_case
                        JOIN "SIGERSA"."SOLICITUD" owner_request ON owner_request.id = owner_case.solicitud_id
                         WHERE owner_case.id = evaluation.caso_id AND owner_request.solicitante_id = @ActorId))
                    OR (@AssignedOnly = true AND (
                        evaluation.evaluador_principal_id = @ActorId
                        OR EXISTS (SELECT 1 FROM "SIGERSA"."ASIGNACION" assignment
                            WHERE assignment.programacion_id = evaluation.programacion_id
                              AND assignment.evaluador_id = @ActorId AND assignment.estado = 'ACTIVA')
                    )))
             ORDER BY evidence.fecha_servidor DESC, evidence.id
             OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
              FROM "SIGERSA"."EVIDENCIA" evidence
              JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.id = evidence.evaluacion_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
             WHERE evidence.eliminada = false
               AND (@EvaluationId IS NULL OR evidence.evaluacion_id = @EvaluationId)
               AND (@EvidenceType IS NULL OR evidence.tipo_evidencia = @EvidenceType)
               AND (@Search IS NULL OR lower(evidence.nombre_original) LIKE '%' || @Search || '%'
                    OR lower(evaluation.numero) LIKE '%' || @Search || '%'
                    OR lower(establishment.nombre) LIKE '%' || @Search || '%')
               AND (@GlobalScope = true
                    OR (@CompanyScope IS NOT NULL AND establishment.empresa_id = @CompanyScope AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."CASO" owner_case
                        JOIN "SIGERSA"."SOLICITUD" owner_request ON owner_request.id = owner_case.solicitud_id
                         WHERE owner_case.id = evaluation.caso_id AND owner_request.solicitante_id = @ActorId))
                    OR (@AssignedOnly = true AND (
                        evaluation.evaluador_principal_id = @ActorId
                        OR EXISTS (SELECT 1 FROM "SIGERSA"."ASIGNACION" assignment
                            WHERE assignment.programacion_id = evaluation.programacion_id
                              AND assignment.evaluador_id = @ActorId AND assignment.estado = 'ACTIVA')
                    )));
            """;
        var parameters = new
        {
            query.Search, query.EvidenceType, query.EvaluationId, query.ActorId, query.CompanyScope,
            query.GlobalScope, query.AssignedOnly,
            Offset = (query.Page - 1) * query.PageSize, query.PageSize
        };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), parameters, cancellationToken: cancellationToken));
            var items = (await result.ReadAsync<EvidenceSummaryRow>()).Select(row => row.ToDomain()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new EvidencesPage(items, query.Page, query.PageSize, total);
        }
    }

    public async Task<EvidenceStorageReference?> GetAuthorizedAsync(
        Guid evidenceId, Guid actorId, Guid? companyScope, bool globalScope, bool assignedOnly,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT evidence.id AS Id, evidence.bucket_name AS BucketName,
                   evidence.supabase_path AS SupabasePath, evidence.nombre_original AS OriginalName,
                   evidence.mime_type AS MimeType
              FROM "SIGERSA"."EVIDENCIA" evidence
              JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.id = evidence.evaluacion_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
             WHERE evidence.id = @EvidenceId AND evidence.eliminada = false
               AND (@GlobalScope = true
                    OR (@CompanyScope IS NOT NULL AND establishment.empresa_id = @CompanyScope AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."CASO" owner_case
                        JOIN "SIGERSA"."SOLICITUD" owner_request ON owner_request.id = owner_case.solicitud_id
                         WHERE owner_case.id = evaluation.caso_id AND owner_request.solicitante_id = @ActorId))
                    OR (@AssignedOnly = true AND (
                        evaluation.evaluador_principal_id = @ActorId
                        OR EXISTS (SELECT 1 FROM "SIGERSA"."ASIGNACION" assignment
                            WHERE assignment.programacion_id = evaluation.programacion_id
                              AND assignment.evaluador_id = @ActorId AND assignment.estado = 'ACTIVA')
                    )));
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.QuerySingleOrDefaultAsync<EvidenceStorageReference>(new CommandDefinition(
                Sql(sql), new { EvidenceId = evidenceId, ActorId = actorId, CompanyScope = companyScope, GlobalScope = globalScope, AssignedOnly = assignedOnly }, cancellationToken: cancellationToken));
        }
    }

    public async Task<bool> CanUploadAsync(Guid userId, Guid evaluationId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM "SIGERSA"."EVALUACION" AS evaluation
                WHERE evaluation.id = @EvaluationId
                  AND evaluation.estado = 'EN_EJECUCION'
                  AND (
                      evaluation.evaluador_principal_id = @UserId
                      OR EXISTS (
                          SELECT 1
                          FROM "SIGERSA"."ASIGNACION" AS assignment
                          WHERE assignment.programacion_id = evaluation.programacion_id
                            AND assignment.evaluador_id = @UserId
                            AND assignment.estado = 'ACTIVA'
                      )
                      OR EXISTS (
                          SELECT 1
                          FROM "SIGERSA"."USUARIO_ROL" AS user_role
                          JOIN "SIGERSA"."ROL" AS role ON role.id = user_role.rol_id
                          WHERE user_role.usuario_id = @UserId
                            AND user_role.activo = true
                            AND role.codigo = 'ADMINISTRADOR'
                            AND role.activo = true
                      )
                  )
            );
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(Sql(sql), new { UserId = userId, EvaluationId = evaluationId }, cancellationToken: cancellationToken));
        }
    }

    public async Task<Guid> CreateAsync(EvidenceRecord evidence, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "SIGERSA"."EVIDENCIA"
                (id, evaluacion_id, subida_por, bucket_name, supabase_path,
                 nombre_original, nombre_seguro, file_size, mime_type, hash,
                 tipo_evidencia, latitud, longitud, precision_m,
                 estado_sincronizacion, idempotency_key, creado_por)
            VALUES
                (@Id, @EvaluationId, @UploadedBy, @BucketName, @SupabasePath,
                 @OriginalName, @SafeName, @FileSize, @MimeType, @Sha256Hash,
                 @EvidenceType, @Latitude, @Longitude, @AccuracyMeters,
                 'SINCRONIZADA', @IdempotencyKey, @UploadedBy)
            ON CONFLICT (idempotency_key) WHERE idempotency_key IS NOT NULL DO UPDATE
            SET id = "SIGERSA"."EVIDENCIA".id
            WHERE "SIGERSA"."EVIDENCIA".evaluacion_id = EXCLUDED.evaluacion_id
              AND "SIGERSA"."EVIDENCIA".subida_por = EXCLUDED.subida_por
              AND "SIGERSA"."EVIDENCIA".bucket_name = EXCLUDED.bucket_name
              AND "SIGERSA"."EVIDENCIA".supabase_path = EXCLUDED.supabase_path
              AND "SIGERSA"."EVIDENCIA".file_size = EXCLUDED.file_size
              AND "SIGERSA"."EVIDENCIA".mime_type = EXCLUDED.mime_type
              AND "SIGERSA"."EVIDENCIA".hash = EXCLUDED.hash
              AND "SIGERSA"."EVIDENCIA".latitud IS NOT DISTINCT FROM EXCLUDED.latitud
              AND "SIGERSA"."EVIDENCIA".longitud IS NOT DISTINCT FROM EXCLUDED.longitud
              AND "SIGERSA"."EVIDENCIA".precision_m IS NOT DISTINCT FROM EXCLUDED.precision_m
            RETURNING id;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            if (evidence.SourceItem is not null)
            {
                await connection.ExecuteAsync(new CommandDefinition(Sql("""
                    SELECT 1 FROM "SIGERSA"."EVALUACION"
                     WHERE id = @EvaluationId FOR UPDATE;
                    """), new { evidence.EvaluationId }, transaction, cancellationToken: cancellationToken));
                var itemEvidenceCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(Sql("""
                    SELECT COUNT(*)::integer
                      FROM "SIGERSA"."EVIDENCIA" existing_evidence
                      JOIN "SIGERSA"."EVIDENCIA_RESPUESTA" evidence_response
                        ON evidence_response.evidencia_id = existing_evidence.id
                      JOIN "SIGERSA"."RESPUESTA_USUARIO" response
                        ON response.id = evidence_response.respuesta_usuario_id
                      JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
                     WHERE existing_evidence.evaluacion_id = @EvaluationId
                       AND existing_evidence.eliminada = false
                       AND item.source_allitems_item = @SourceItem
                       AND (@IdempotencyKey IS NULL OR existing_evidence.idempotency_key IS DISTINCT FROM @IdempotencyKey);
                    """), new { evidence.EvaluationId, evidence.SourceItem, evidence.IdempotencyKey }, transaction,
                    cancellationToken: cancellationToken));
                if (itemEvidenceCount >= 3)
                    throw new InvalidOperationException("Cada ítem admite un máximo de 3 evidencias.");
            }
            var id = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                Sql(sql), evidence, transaction, cancellationToken: cancellationToken));
            var persistedId = id ?? throw new InvalidOperationException(
                "La clave de idempotencia ya fue utilizada con otros metadatos.");
            if (evidence.SourceItem is not null)
            {
                var linked = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                    INSERT INTO "SIGERSA"."EVIDENCIA_RESPUESTA"
                        (evidencia_id, respuesta_usuario_id, creado_por)
                    SELECT @EvidenceId, response.id, @UploadedBy
                      FROM "SIGERSA"."RESPUESTA_USUARIO" response
                      JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
                     WHERE response.evaluacion_id = @EvaluationId
                       AND item.source_allitems_item = @SourceItem
                    ON CONFLICT (evidencia_id, respuesta_usuario_id) DO NOTHING;
                    """), new { EvidenceId = persistedId, evidence.UploadedBy, evidence.EvaluationId, evidence.SourceItem },
                    transaction, cancellationToken: cancellationToken));
                if (linked == 0)
                {
                    var associationExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(Sql("""
                        SELECT EXISTS (
                            SELECT 1
                              FROM "SIGERSA"."EVIDENCIA_RESPUESTA" evidence_response
                              JOIN "SIGERSA"."RESPUESTA_USUARIO" response
                                ON response.id = evidence_response.respuesta_usuario_id
                              JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
                             WHERE evidence_response.evidencia_id = @EvidenceId
                               AND response.evaluacion_id = @EvaluationId
                               AND item.source_allitems_item = @SourceItem
                        );
                        """), new { EvidenceId = persistedId, evidence.EvaluationId, evidence.SourceItem },
                        transaction, cancellationToken: cancellationToken));
                    if (!associationExists)
                        throw new InvalidOperationException("Debe guardar la respuesta antes de asociarle una evidencia.");
                }
                await connection.ExecuteAsync(new CommandDefinition(Sql("""
                    INSERT INTO "SIGERSA"."EVIDENCIA_NO_CONFORMIDAD"
                        (evidencia_id, no_conformidad_id, creado_por)
                    SELECT @EvidenceId, finding.id, @UploadedBy
                      FROM "SIGERSA"."NO_CONFORMIDAD" finding
                      JOIN "SIGERSA"."RESPUESTA_USUARIO" response
                        ON response.id = finding.respuesta_usuario_id
                      JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
                     WHERE response.evaluacion_id = @EvaluationId
                       AND item.source_allitems_item = @SourceItem
                    ON CONFLICT (evidencia_id, no_conformidad_id) DO NOTHING;
                    """), new { EvidenceId = persistedId, evidence.UploadedBy, evidence.EvaluationId, evidence.SourceItem },
                    transaction, cancellationToken: cancellationToken));
            }
            await transaction.CommitAsync(cancellationToken);
            return persistedId;
        }
    }

    private sealed class EvidenceSummaryRow
    {
        public Guid Id { get; init; } public Guid EvaluationId { get; init; }
        public string EvaluationNumber { get; init; } = string.Empty; public string EstablishmentName { get; init; } = string.Empty;
        public Guid UploadedBy { get; init; } public string UploadedByName { get; init; } = string.Empty;
        public string OriginalName { get; init; } = string.Empty; public long FileSize { get; init; }
        public string MimeType { get; init; } = string.Empty; public string EvidenceType { get; init; } = string.Empty;
        public string SynchronizationStatus { get; init; } = string.Empty; public DateTime UploadedAt { get; init; }
        public int? SourceItem { get; init; } public string? ItemCode { get; init; } public string? ItemTitle { get; init; }
        public EvidenceSummary ToDomain() => new(Id, EvaluationId, EvaluationNumber, EstablishmentName,
            UploadedBy, UploadedByName, OriginalName, FileSize, MimeType, EvidenceType,
            SynchronizationStatus, new DateTimeOffset(DateTime.SpecifyKind(UploadedAt, DateTimeKind.Utc)),
            SourceItem, ItemCode, ItemTitle);
    }
}
