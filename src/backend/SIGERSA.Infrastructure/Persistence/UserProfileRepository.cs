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
                   estado AS Status, ultimo_acceso_en AS LastAccessAt,
                   supabase_auth_user_id AS SupabaseAuthUserId,
                   mfa_habilitado AS MfaEnabled, mfa_factor_id AS MfaFactorId,
                   version_fila AS RowVersion
            FROM "SIGERSA"."USUARIO"
            WHERE id = @UserId AND activo = true;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var row = await connection.QuerySingleOrDefaultAsync<UserProfileRow>(
                new CommandDefinition(Sql(sql), new { UserId = userId }, cancellationToken: cancellationToken));
            return row?.ToDomain();
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

    public async Task SetSupabaseIdentityAsync(
        Guid userId,
        Guid supabaseUserId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."USUARIO"
               SET supabase_auth_user_id = @SupabaseUserId,
                   modificado_en = CURRENT_TIMESTAMP,
                   modificado_por = @UserId,
                   version_fila = version_fila + 1
             WHERE id = @UserId AND activo = true;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                Sql(sql), new { UserId = userId, SupabaseUserId = supabaseUserId },
                cancellationToken: cancellationToken));
            if (affected != 1) throw new KeyNotFoundException("El usuario no está disponible.");
        }
    }

    public async Task SetMfaAsync(
        Guid userId,
        bool enabled,
        Guid? factorId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."USUARIO"
               SET mfa_habilitado = @Enabled,
                   mfa_factor_id = CASE WHEN @Enabled THEN @FactorId ELSE NULL END,
                   modificado_en = CURRENT_TIMESTAMP,
                   modificado_por = @UserId,
                   version_fila = version_fila + 1
             WHERE id = @UserId AND activo = true
               AND supabase_auth_user_id IS NOT NULL;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                Sql(sql), new { UserId = userId, Enabled = enabled, FactorId = factorId },
                cancellationToken: cancellationToken));
            if (affected != 1) throw new KeyNotFoundException("La identidad MFA no está configurada.");
        }
    }

    private sealed class UserProfileRow
    {
        public Guid Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? Phone { get; init; }
        public string PasswordHash { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime? LastAccessAt { get; init; }
        public Guid? SupabaseAuthUserId { get; init; }
        public bool MfaEnabled { get; init; }
        public Guid? MfaFactorId { get; init; }
        public long RowVersion { get; init; }

        public UserProfileAccount ToDomain() => new(
            Id,
            FullName,
            Email,
            Phone,
            PasswordHash,
            Status,
            LastAccessAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(LastAccessAt.Value, DateTimeKind.Utc)),
            SupabaseAuthUserId,
            MfaEnabled,
            MfaFactorId,
            RowVersion);
    }
}
