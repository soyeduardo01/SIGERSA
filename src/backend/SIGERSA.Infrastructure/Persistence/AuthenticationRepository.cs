using System.Data.Common;
using Dapper;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Security;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class AuthenticationRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IAuthenticationRepository
{
    public async Task<AuthenticationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT u.id AS Id, u.empresa_id AS EmpresaId, u.nombre_completo AS NombreCompleto,
                   u.correo AS Correo, u.password_hash AS PasswordHash, u.estado AS Estado,
                   u.activo AS Activo, u.fallos_acceso AS FallosAcceso, u.bloqueado_hasta AS BloqueadoHasta,
                   COALESCE(array_agg(DISTINCT r.codigo) FILTER (
                       WHERE r.codigo IS NOT NULL AND ur.activo = true
                         AND ur.vigente_desde <= CURRENT_TIMESTAMP
                         AND (ur.vigente_hasta IS NULL OR ur.vigente_hasta > CURRENT_TIMESTAMP)
                   ), ARRAY[]::varchar[]) AS Roles
            FROM "SIGERSA"."USUARIO" AS u
            LEFT JOIN "SIGERSA"."USUARIO_ROL" AS ur ON ur.usuario_id = u.id
            LEFT JOIN "SIGERSA"."ROL" AS r ON r.id = ur.rol_id AND r.activo = true
            WHERE u.correo_normalizado = @NormalizedEmail
            GROUP BY u.id;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var row = await connection.QuerySingleOrDefaultAsync<AuthenticationUserRow>(
                new CommandDefinition(Sql(sql), new { NormalizedEmail = normalizedEmail }, cancellationToken: cancellationToken));
            return row?.ToDomain();
        }
    }

    public Task RecordFailedLoginAsync(Guid userId, int maximumAttempts, DateTimeOffset blockedUntil, CancellationToken cancellationToken = default) =>
        ExecuteNoResultAsync("""
            UPDATE "SIGERSA"."USUARIO"
               SET fallos_acceso = fallos_acceso + 1,
                   bloqueado_hasta = CASE WHEN fallos_acceso + 1 >= @MaximumAttempts THEN @BlockedUntil ELSE bloqueado_hasta END,
                   estado = CASE WHEN fallos_acceso + 1 >= @MaximumAttempts THEN 'BLOQUEADO' ELSE estado END,
                   modificado_en = CURRENT_TIMESTAMP, modificado_por = @UserId,
                   version_fila = version_fila + 1
             WHERE id = @UserId;
            """, new { UserId = userId, MaximumAttempts = maximumAttempts, BlockedUntil = blockedUntil }, cancellationToken);

    public Task RecordSuccessfulLoginAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        ExecuteNoResultAsync("""
            UPDATE "SIGERSA"."USUARIO"
               SET ultimo_acceso_en = @Now, fallos_acceso = 0, bloqueado_hasta = NULL,
                   estado = 'ACTIVO', modificado_en = @Now, modificado_por = @UserId,
                   version_fila = version_fila + 1
             WHERE id = @UserId;
            """, new { UserId = userId, Now = now }, cancellationToken);

    public Task InvalidateActiveOtpsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        ExecuteNoResultAsync("""
            UPDATE "SIGERSA"."OTP_RECUPERACION"
               SET estado = 'INVALIDADO', invalidado_en = @Now, modificado_en = @Now,
                   modificado_por = @UserId, version_fila = version_fila + 1
             WHERE usuario_id = @UserId AND estado = 'ACTIVO' AND proposito = 'RECUPERACION';
            """, new { UserId = userId, Now = now }, cancellationToken);

    public Task CreateOtpAsync(Guid userId, string otpHash, DateTimeOffset expiresAt, string? ipHash, CancellationToken cancellationToken = default) =>
        ExecuteNoResultAsync("""
            INSERT INTO "SIGERSA"."OTP_RECUPERACION"
                (id, usuario_id, otp_hash, expira_en, estado, ip_hash, proposito, creado_por)
            VALUES (@Id, @UserId, @OtpHash, @ExpiresAt, 'ACTIVO', @IpHash, 'RECUPERACION', @UserId);
            """, new { Id = Guid.NewGuid(), UserId = userId, OtpHash = otpHash, ExpiresAt = expiresAt, IpHash = ipHash }, cancellationToken);

    public async Task<OtpChallenge?> GetLatestActiveOtpAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, usuario_id AS UsuarioId, otp_hash AS OtpHash,
                   expira_en AS ExpiraEn, intentos AS Intentos, estado AS Estado
            FROM "SIGERSA"."OTP_RECUPERACION"
            WHERE usuario_id = @UserId AND estado = 'ACTIVO' AND proposito = 'RECUPERACION'
            ORDER BY creado_en DESC LIMIT 1;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var row = await connection.QuerySingleOrDefaultAsync<OtpChallengeRow>(
                new CommandDefinition(Sql(sql), new { UserId = userId }, cancellationToken: cancellationToken));
            return row?.ToDomain();
        }
    }

    public Task InvalidateActiveTwoFactorOtpsAsync(
        Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        ExecuteNoResultAsync("""
            UPDATE "SIGERSA"."OTP_RECUPERACION"
               SET estado = 'INVALIDADO', invalidado_en = @Now, modificado_en = @Now,
                   modificado_por = @UserId, version_fila = version_fila + 1
             WHERE usuario_id = @UserId AND estado = 'ACTIVO' AND proposito = 'DOS_FACTORES';
            """, new { UserId = userId, Now = now }, cancellationToken);

    public Task CreateTwoFactorOtpAsync(
        Guid userId, string otpHash, DateTimeOffset expiresAt, string? ipHash,
        CancellationToken cancellationToken = default) =>
        ExecuteNoResultAsync("""
            INSERT INTO "SIGERSA"."OTP_RECUPERACION"
                (id, usuario_id, otp_hash, expira_en, estado, ip_hash, proposito, creado_por)
            VALUES (@Id, @UserId, @OtpHash, @ExpiresAt, 'ACTIVO', @IpHash, 'DOS_FACTORES', @UserId);
            """, new { Id = Guid.NewGuid(), UserId = userId, OtpHash = otpHash, ExpiresAt = expiresAt, IpHash = ipHash }, cancellationToken);

    public async Task<OtpChallenge?> GetLatestActiveTwoFactorOtpAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, usuario_id AS UsuarioId, otp_hash AS OtpHash,
                   expira_en AS ExpiraEn, intentos AS Intentos, estado AS Estado
              FROM "SIGERSA"."OTP_RECUPERACION"
             WHERE usuario_id = @UserId AND estado = 'ACTIVO' AND proposito = 'DOS_FACTORES'
             ORDER BY creado_en DESC LIMIT 1;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var row = await connection.QuerySingleOrDefaultAsync<OtpChallengeRow>(
                new CommandDefinition(Sql(sql), new { UserId = userId }, cancellationToken: cancellationToken));
            return row?.ToDomain();
        }
    }

    public Task RecordFailedOtpAttemptAsync(Guid otpId, int maximumAttempts, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        ExecuteNoResultAsync("""
            UPDATE "SIGERSA"."OTP_RECUPERACION"
               SET intentos = intentos + 1,
                   estado = CASE WHEN expira_en <= @Now THEN 'EXPIRADO'
                                 WHEN intentos + 1 >= @MaximumAttempts THEN 'BLOQUEADO'
                                 ELSE estado END,
                   modificado_en = @Now, version_fila = version_fila + 1
             WHERE id = @OtpId AND estado = 'ACTIVO';
            """, new { OtpId = otpId, MaximumAttempts = maximumAttempts, Now = now }, cancellationToken);

    public async Task<bool> ConsumeOtpAsync(Guid otpId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("""
            UPDATE "SIGERSA"."OTP_RECUPERACION"
               SET estado = 'USADO', usado_en = @Now, modificado_en = @Now,
                   version_fila = version_fila + 1
             WHERE id = @OtpId AND estado = 'ACTIVO' AND expira_en > @Now;
            """, new { OtpId = otpId, Now = now }, cancellationToken) == 1;

    public Task StorePasswordResetProofAsync(Guid proofId, Guid userId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) =>
        ExecuteNoResultAsync("""
            INSERT INTO "SIGERSA"."COMPROBANTE_RECUPERACION" (id, usuario_id, expira_en)
            VALUES (@ProofId, @UserId, @ExpiresAt);
            """, new { ProofId = proofId, UserId = userId, ExpiresAt = expiresAt }, cancellationToken);

    public Task StoreRefreshTokenAsync(Guid userId, string tokenHash, Guid familyId, DateTimeOffset expiresAt, string? device, string? ipHash, CancellationToken cancellationToken = default) =>
        ExecuteNoResultAsync("""
            INSERT INTO "SIGERSA"."REFRESH_TOKEN"
                (id, usuario_id, token_hash, familia_token_id, expira_en, dispositivo, ip_hash, creado_por)
            VALUES (@Id, @UserId, @TokenHash, @FamilyId, @ExpiresAt, @Device, @IpHash, @UserId);
            """, new { Id = Guid.NewGuid(), UserId = userId, TokenHash = tokenHash, FamilyId = familyId, ExpiresAt = expiresAt, Device = device, IpHash = ipHash }, cancellationToken);

    public async Task<AuthenticationUser?> RotateRefreshTokenAsync(
        string currentTokenHash, string replacementTokenHash, DateTimeOffset replacementExpiresAt,
        DateTimeOffset now, string? device, string? ipHash, CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            const string selectSql = """
                SELECT id AS Id, usuario_id AS UserId, familia_token_id AS FamilyId,
                       expira_en AS ExpiresAt, revocado_en AS RevokedAt
                FROM "SIGERSA"."REFRESH_TOKEN"
                WHERE token_hash = @TokenHash FOR UPDATE;
                """;
            var current = await connection.QuerySingleOrDefaultAsync<RefreshRow>(
                new CommandDefinition(Sql(selectSql), new { TokenHash = currentTokenHash }, transaction, cancellationToken: cancellationToken));
            if (current is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            if (current.RevokedAt is not null)
            {
                await connection.ExecuteAsync(new CommandDefinition(Sql("""
                    UPDATE "SIGERSA"."REFRESH_TOKEN"
                       SET revocado_en = COALESCE(revocado_en, GREATEST(@Now, creado_en)),
                           modificado_en = GREATEST(@Now, creado_en),
                           version_fila = version_fila + 1
                     WHERE familia_token_id = @FamilyId;
                    """), new { current.FamilyId, Now = now }, transaction, cancellationToken: cancellationToken));
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            if (Utc(current.ExpiresAt) <= now)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var user = await FindByIdAsync(connection, transaction, current.UserId, cancellationToken);
            if (user is null || !user.Activo || user.Estado is not "ACTIVO")
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var replacementId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."REFRESH_TOKEN"
                    (id, usuario_id, token_hash, familia_token_id, expira_en, dispositivo, ip_hash, creado_por)
                VALUES (@ReplacementId, @UserId, @ReplacementHash, @FamilyId, @ExpiresAt, @Device, @IpHash, @UserId);
                """), new { ReplacementId = replacementId, current.UserId, ReplacementHash = replacementTokenHash, current.FamilyId, ExpiresAt = replacementExpiresAt, Device = device, IpHash = ipHash }, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."REFRESH_TOKEN"
                   SET revocado_en = @Now, reemplazado_por_id = @ReplacementId,
                       modificado_en = @Now, modificado_por = @UserId,
                       version_fila = version_fila + 1
                 WHERE id = @Id;
                """), new { current.Id, ReplacementId = replacementId, Now = now, current.UserId }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return user;
        }
    }

    public async Task<bool> ConsumePasswordResetProofAndUpdatePasswordAsync(
        Guid proofId,
        Guid userId,
        string passwordHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var proofConsumed = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."COMPROBANTE_RECUPERACION"
                   SET usado_en = @Now
                 WHERE id = @ProofId AND usuario_id = @UserId
                   AND usado_en IS NULL AND expira_en > @Now;
                """), new { ProofId = proofId, UserId = userId, Now = now }, transaction, cancellationToken: cancellationToken));
            if (proofConsumed != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var affected = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."USUARIO"
                   SET password_hash = @PasswordHash, fallos_acceso = 0, bloqueado_hasta = NULL,
                       estado = 'ACTIVO', modificado_en = @Now, modificado_por = @UserId,
                       version_fila = version_fila + 1
                 WHERE id = @UserId AND activo = true;
                """), new { UserId = userId, PasswordHash = passwordHash, Now = now }, transaction, cancellationToken: cancellationToken));
            if (affected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."REFRESH_TOKEN"
                   SET revocado_en = COALESCE(revocado_en, @Now), modificado_en = @Now,
                       modificado_por = @UserId, version_fila = version_fila + 1
                 WHERE usuario_id = @UserId;
                """), new { UserId = userId, Now = now }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }

    public Task RevokeRefreshTokenAsync(Guid userId, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        ExecuteNoResultAsync("""
            UPDATE "SIGERSA"."REFRESH_TOKEN"
               SET revocado_en = COALESCE(revocado_en, @Now), modificado_en = @Now,
                   modificado_por = @UserId, version_fila = version_fila + 1
             WHERE usuario_id = @UserId AND token_hash = @TokenHash AND revocado_en IS NULL;
            """, new { UserId = userId, TokenHash = tokenHash, Now = now }, cancellationToken);

    private async Task ExecuteNoResultAsync(string sql, object parameters, CancellationToken cancellationToken) =>
        _ = await ExecuteAsync(sql, parameters, cancellationToken);

    private async Task<int> ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.ExecuteAsync(new CommandDefinition(Sql(sql), parameters, cancellationToken: cancellationToken));
        }
    }

    private static async Task<AuthenticationUser?> FindByIdAsync(DbConnection connection, DbTransaction transaction, Guid userId, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<AuthenticationUserRow>(new CommandDefinition(Sql("""
            SELECT u.id AS Id, u.empresa_id AS EmpresaId, u.nombre_completo AS NombreCompleto,
                   u.correo AS Correo, u.password_hash AS PasswordHash, u.estado AS Estado,
                   u.activo AS Activo, u.fallos_acceso AS FallosAcceso, u.bloqueado_hasta AS BloqueadoHasta,
                   COALESCE(array_agg(DISTINCT r.codigo) FILTER (
                       WHERE r.codigo IS NOT NULL AND ur.activo = true
                         AND ur.vigente_desde <= CURRENT_TIMESTAMP
                         AND (ur.vigente_hasta IS NULL OR ur.vigente_hasta > CURRENT_TIMESTAMP)
                   ), ARRAY[]::varchar[]) AS Roles
            FROM "SIGERSA"."USUARIO" AS u
            LEFT JOIN "SIGERSA"."USUARIO_ROL" AS ur ON ur.usuario_id = u.id
            LEFT JOIN "SIGERSA"."ROL" AS r ON r.id = ur.rol_id AND r.activo = true
            WHERE u.id = @UserId GROUP BY u.id;
            """), new { UserId = userId }, transaction, cancellationToken: cancellationToken));
        return row?.ToDomain();
    }

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed class AuthenticationUserRow
    {
        public Guid Id { get; init; }
        public Guid? EmpresaId { get; init; }
        public string NombreCompleto { get; init; } = string.Empty;
        public string Correo { get; init; } = string.Empty;
        public string PasswordHash { get; init; } = string.Empty;
        public string Estado { get; init; } = string.Empty;
        public bool Activo { get; init; }
        public int FallosAcceso { get; init; }
        public DateTime? BloqueadoHasta { get; init; }
        public string[] Roles { get; init; } = [];

        public AuthenticationUser ToDomain() => new()
        {
            Id = Id,
            EmpresaId = EmpresaId,
            NombreCompleto = NombreCompleto,
            Correo = Correo,
            PasswordHash = PasswordHash,
            Estado = Estado,
            Activo = Activo,
            FallosAcceso = FallosAcceso,
            BloqueadoHasta = BloqueadoHasta is null ? null : Utc(BloqueadoHasta.Value),
            Roles = Roles
        };
    }

    private sealed class OtpChallengeRow
    {
        public Guid Id { get; init; }
        public Guid UsuarioId { get; init; }
        public string OtpHash { get; init; } = string.Empty;
        public DateTime ExpiraEn { get; init; }
        public int Intentos { get; init; }
        public string Estado { get; init; } = string.Empty;

        public OtpChallenge ToDomain() => new(Id, UsuarioId, OtpHash, Utc(ExpiraEn), Intentos, Estado);
    }

    private sealed class RefreshRow
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public Guid FamilyId { get; init; }
        public DateTime ExpiresAt { get; init; }
        public DateTime? RevokedAt { get; init; }
    }
}
