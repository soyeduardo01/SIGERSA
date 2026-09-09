using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class EvidenceRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IEvidenceRepository
{
    public async Task<bool> CanUploadAsync(Guid userId, Guid evaluationId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM "SIGERSA"."EVALUACION" AS evaluation
                WHERE evaluation.id = @EvaluationId
                  AND evaluation.estado IN ('ASIGNADA', 'EN_EJECUCION', 'PAUSADA', 'EN_CORRECCION')
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
                 tipo_evidencia, estado_sincronizacion, idempotency_key, creado_por)
            VALUES
                (@Id, @EvaluationId, @UploadedBy, @BucketName, @SupabasePath,
                 @OriginalName, @SafeName, @FileSize, @MimeType, @Sha256Hash,
                 @EvidenceType, 'SINCRONIZADA', @IdempotencyKey, @UploadedBy)
            ON CONFLICT (idempotency_key) WHERE idempotency_key IS NOT NULL DO UPDATE
            SET id = "SIGERSA"."EVIDENCIA".id
            WHERE "SIGERSA"."EVIDENCIA".evaluacion_id = EXCLUDED.evaluacion_id
              AND "SIGERSA"."EVIDENCIA".subida_por = EXCLUDED.subida_por
              AND "SIGERSA"."EVIDENCIA".bucket_name = EXCLUDED.bucket_name
              AND "SIGERSA"."EVIDENCIA".supabase_path = EXCLUDED.supabase_path
              AND "SIGERSA"."EVIDENCIA".file_size = EXCLUDED.file_size
              AND "SIGERSA"."EVIDENCIA".mime_type = EXCLUDED.mime_type
              AND "SIGERSA"."EVIDENCIA".hash = EXCLUDED.hash
            RETURNING id;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var id = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(Sql(sql), evidence, cancellationToken: cancellationToken));
            return id ?? throw new InvalidOperationException("La clave de idempotencia ya fue utilizada con otros metadatos.");
        }
    }
}
