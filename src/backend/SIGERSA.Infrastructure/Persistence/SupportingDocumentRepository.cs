using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class SupportingDocumentRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), ISupportingDocumentRepository
{
    public async Task<bool> CanAttachToUserAsync(Guid targetUserId, Guid? companyScope, bool globalScope,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM "SIGERSA"."USUARIO"
                 WHERE id = @TargetUserId AND activo = true
                   AND (@GlobalScope = true OR empresa_id = @CompanyScope));
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
            return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                Sql(sql), new { TargetUserId = targetUserId, CompanyScope = companyScope, GlobalScope = globalScope },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> CanAttachToRequestAsync(Guid requestId, Guid actorId, Guid? companyScope,
        bool globalScope, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM "SIGERSA"."SOLICITUD"
                 WHERE id = @RequestId AND activo = true AND estado = 'BORRADOR'
                   AND (@GlobalScope = true OR empresa_id = @CompanyScope)
                   AND (@GlobalScope = true OR solicitante_id = @ActorId OR empresa_id = @CompanyScope));
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
            return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                Sql(sql), new { RequestId = requestId, ActorId = actorId, CompanyScope = companyScope, GlobalScope = globalScope },
                cancellationToken: cancellationToken));
    }

    public async Task<Guid> SaveUserAuthorizationAsync(Guid userId, SupportingDocument document,
        Guid actorId, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."USUARIO_DOCUMENTO_AUTORIZACION"
                   SET vigente = false, modificado_por = @ActorId
                 WHERE usuario_id = @UserId AND vigente = true;
                """), new { UserId = userId, ActorId = actorId }, transaction,
                cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."USUARIO_DOCUMENTO_AUTORIZACION"
                    (id, usuario_id, bucket_name, supabase_path, nombre_original,
                     file_size, mime_type, hash, vigente, creado_por)
                VALUES (@Id, @UserId, @BucketName, @SupabasePath, @OriginalName,
                        @FileSize, @MimeType, @Sha256Hash, true, @ActorId);
                """), new { document.Id, UserId = userId, document.BucketName, document.SupabasePath,
                    document.OriginalName, document.FileSize, document.MimeType, document.Sha256Hash,
                    ActorId = actorId }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return document.Id;
        }
    }

    public async Task<Guid> SaveRequestDocumentAsync(Guid requestId, string documentType, bool required,
        SupportingDocument document, Guid actorId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "SIGERSA"."SOLICITUD_DOCUMENTO"
                (id, solicitud_id, tipo_documento, bucket_name, supabase_path,
                 nombre_original, file_size, mime_type, hash, obligatorio, activo, creado_por)
            VALUES (@Id, @RequestId, @DocumentType, @BucketName, @SupabasePath,
                    @OriginalName, @FileSize, @MimeType, @Sha256Hash, @Required, true, @ActorId);
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            await connection.ExecuteAsync(new CommandDefinition(Sql(sql), new { document.Id,
                RequestId = requestId, DocumentType = documentType, document.BucketName,
                document.SupabasePath, document.OriginalName, document.FileSize, document.MimeType,
                document.Sha256Hash, Required = required, ActorId = actorId }, cancellationToken: cancellationToken));
            return document.Id;
        }
    }

    public async Task<SupportingDocumentReference?> GetUserAuthorizationAsync(
        Guid userId, Guid? companyScope, bool globalScope,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT document.bucket_name AS BucketName,
                   document.supabase_path AS SupabasePath,
                   document.nombre_original AS OriginalName,
                   document.mime_type AS MimeType
              FROM "SIGERSA"."USUARIO_DOCUMENTO_AUTORIZACION" document
              JOIN "SIGERSA"."USUARIO" user_account ON user_account.id = document.usuario_id
             WHERE document.usuario_id = @UserId AND document.vigente = true
               AND (@GlobalScope = true OR user_account.empresa_id = @CompanyScope)
             ORDER BY document.creado_en DESC
             LIMIT 1;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
            return await connection.QuerySingleOrDefaultAsync<SupportingDocumentReference>(
                new CommandDefinition(Sql(sql), new { UserId = userId, CompanyScope = companyScope, GlobalScope = globalScope },
                    cancellationToken: cancellationToken));
    }
}
