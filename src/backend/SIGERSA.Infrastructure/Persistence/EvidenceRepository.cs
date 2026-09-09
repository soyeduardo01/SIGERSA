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
                 tipo_evidencia, estado_sincronizacion, creado_por)
            VALUES
                (@Id, @EvaluationId, @UploadedBy, @BucketName, @SupabasePath,
                 @OriginalName, @SafeName, @FileSize, @MimeType, @Sha256Hash,
                 @EvidenceType, 'SINCRONIZADA', @UploadedBy)
            RETURNING id;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(Sql(sql), evidence, cancellationToken: cancellationToken));
        }
    }
}
