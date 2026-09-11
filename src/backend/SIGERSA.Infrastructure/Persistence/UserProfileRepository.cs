using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class UserProfileRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IUserProfileRepository
{
    public async Task<UserProfileAccount?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, nombre_completo AS FullName, correo AS Email,
                   telefono AS Phone, password_hash AS PasswordHash,
                   version_fila AS RowVersion
            FROM "SIGERSA"."USUARIO"
            WHERE id = @UserId AND activo = true;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.QuerySingleOrDefaultAsync<UserProfileAccount>(
                new CommandDefinition(Sql(sql), new { UserId = userId }, cancellationToken: cancellationToken));
        }
    }

    public async Task<bool> UpdateAsync(
        Guid userId,
        UserProfileDraft draft,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."USUARIO"
               SET nombre_completo = @FullName,
                   correo = @Email,
                   correo_normalizado = @Email,
                   telefono = @Phone,
                   modificado_en = CURRENT_TIMESTAMP,
                   modificado_por = @UserId,
                   version_fila = version_fila + 1
             WHERE id = @UserId AND activo = true AND version_fila = @RowVersion;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                Sql(sql),
                new
                {
                    UserId = userId,
                    draft.FullName,
                    draft.Email,
                    draft.Phone,
                    draft.RowVersion
                },
                cancellationToken: cancellationToken));
            return affected == 1;
        }
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId,
        string passwordHash,
        long rowVersion,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."USUARIO"
                   SET password_hash = @PasswordHash,
                       fallos_acceso = 0,
                       bloqueado_hasta = NULL,
                       modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @UserId,
                       version_fila = version_fila + 1
                 WHERE id = @UserId AND activo = true AND version_fila = @RowVersion;
                """), new { UserId = userId, PasswordHash = passwordHash, RowVersion = rowVersion }, transaction,
                cancellationToken: cancellationToken));
            if (affected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."REFRESH_TOKEN"
                   SET revocado_en = COALESCE(revocado_en, CURRENT_TIMESTAMP),
                       modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @UserId,
                       version_fila = version_fila + 1
                 WHERE usuario_id = @UserId AND revocado_en IS NULL;
                """), new { UserId = userId }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }
}
