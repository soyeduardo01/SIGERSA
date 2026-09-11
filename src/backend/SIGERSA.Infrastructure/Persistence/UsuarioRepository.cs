using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Security;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class UsuarioRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IUsuarioRepository
{
    public async Task<Usuario?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id AS Id,
                correo AS Correo,
                nombre_completo AS NombreCompleto,
                activo AS Activo,
                creado_en AS CreadoEn,
                creado_por AS CreadoPor,
                modificado_en AS ModificadoEn,
                modificado_por AS ModificadoPor,
                version_fila AS VersionFila
            FROM "SIGERSA"."USUARIO"
            WHERE id = @Id;
            """;

        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var command = new CommandDefinition(
                Sql(sql),
                new { Id = id },
                cancellationToken: cancellationToken);

            var row = await connection.QuerySingleOrDefaultAsync<UsuarioRow>(command);
            return row?.ToDomain();
        }
    }

    public Task ActualizarNombreAsync(
        Guid id,
        string nombreCompleto,
        Guid modificadoPor,
        long versionFila,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."USUARIO"
            SET nombre_completo = @NombreCompleto,
                modificado_en = CURRENT_TIMESTAMP,
                modificado_por = @ModificadoPor,
                version_fila = version_fila + 1
            WHERE id = @Id
              AND version_fila = @VersionFila;
            """;

        return ExecuteConcurrencyCheckedAsync(
            sql,
            new { Id = id, NombreCompleto = nombreCompleto, ModificadoPor = modificadoPor, VersionFila = versionFila },
            id,
            cancellationToken);
    }

    public async Task<ManagedUsersPage> SearchManagedAsync(
        ManagedUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT u.id AS Id, u.nombre_completo AS NombreCompleto, u.correo AS Correo,
                   u.tipo_identificacion AS TipoIdentificacion,
                   u.identificacion_normalizada AS Identificacion, u.telefono AS Telefono,
                   COALESCE(array_agg(DISTINCT r.codigo) FILTER (
                       WHERE r.codigo IS NOT NULL AND ur.activo = true
                         AND ur.vigente_desde <= CURRENT_TIMESTAMP
                         AND (ur.vigente_hasta IS NULL OR ur.vigente_hasta > CURRENT_TIMESTAMP)
                   ), array_remove(ARRAY[u.rol_solicitado]::varchar[], NULL)) AS Roles,
                   u.empresa_id AS EmpresaId, e.razon_social AS EmpresaNombre,
                   u.estado AS Estado, u.activo AS Activo, u.version_fila AS VersionFila
            FROM "SIGERSA"."USUARIO" AS u
            LEFT JOIN "SIGERSA"."EMPRESA" AS e ON e.id = u.empresa_id
            LEFT JOIN "SIGERSA"."USUARIO_ROL" AS ur ON ur.usuario_id = u.id
            LEFT JOIN "SIGERSA"."ROL" AS r ON r.id = ur.rol_id
            WHERE (@CompanyScope IS NULL OR u.empresa_id = @CompanyScope)
              AND (@Search IS NULL OR lower(u.nombre_completo) LIKE '%' || @Search || '%'
                   OR lower(u.correo) LIKE '%' || @Search || '%'
                   OR lower(u.identificacion_normalizada) LIKE '%' || @Search || '%')
              AND (@Role IS NULL OR u.rol_solicitado = @Role OR EXISTS (
                    SELECT 1 FROM "SIGERSA"."USUARIO_ROL" AS filter_ur
                    JOIN "SIGERSA"."ROL" AS filter_r ON filter_r.id = filter_ur.rol_id
                    WHERE filter_ur.usuario_id = u.id AND filter_ur.activo = true
                      AND filter_r.codigo = @Role))
              AND (@Status IS NULL OR u.estado = @Status)
            GROUP BY u.id, e.razon_social
            ORDER BY u.nombre_completo, u.id
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM "SIGERSA"."USUARIO" AS u
            WHERE (@CompanyScope IS NULL OR u.empresa_id = @CompanyScope)
              AND (@Search IS NULL OR lower(u.nombre_completo) LIKE '%' || @Search || '%'
                   OR lower(u.correo) LIKE '%' || @Search || '%'
                   OR lower(u.identificacion_normalizada) LIKE '%' || @Search || '%')
              AND (@Role IS NULL OR u.rol_solicitado = @Role OR EXISTS (
                    SELECT 1 FROM "SIGERSA"."USUARIO_ROL" AS filter_ur
                    JOIN "SIGERSA"."ROL" AS filter_r ON filter_r.id = filter_ur.rol_id
                    WHERE filter_ur.usuario_id = u.id AND filter_ur.activo = true
                      AND filter_r.codigo = @Role))
              AND (@Status IS NULL OR u.estado = @Status);
            """;
        var parameters = new
        {
            query.Search,
            query.Role,
            query.Status,
            query.CompanyScope,
            Offset = (query.Page - 1) * query.PageSize,
            query.PageSize
        };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(
                new CommandDefinition(Sql(sql), parameters, cancellationToken: cancellationToken));
            var rows = (await result.ReadAsync<ManagedUserRow>()).Select(row => row.ToDomain()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new ManagedUsersPage(rows, query.Page, query.PageSize, total);
        }
    }

    public async Task<IReadOnlyList<RoleOption>> GetActiveRoleOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo AS Code, nombre AS Name
            FROM "SIGERSA"."ROL"
            WHERE activo = true
            ORDER BY nombre;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var rows = await connection.QueryAsync<RoleOptionRow>(
                new CommandDefinition(Sql(sql), cancellationToken: cancellationToken));
            return rows.Select(row => new RoleOption(row.Code, row.Name)).ToArray();
        }
    }

    public async Task<IReadOnlyList<CompanyOption>> GetActiveCompanyOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, razon_social AS Name
            FROM "SIGERSA"."EMPRESA"
            WHERE activo = true
            ORDER BY razon_social;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var rows = await connection.QueryAsync<CompanyOptionRow>(
                new CommandDefinition(Sql(sql), cancellationToken: cancellationToken));
            return rows.Select(row => new CompanyOption(row.Id, row.Name)).ToArray();
        }
    }

    public async Task<bool> CanActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM "SIGERSA"."USUARIO" user_account
                 WHERE user_account.id = @Id AND user_account.activo = true
                   AND (
                       (user_account.rol_solicitado IS NULL AND NOT EXISTS (
                           SELECT 1 FROM "SIGERSA"."USUARIO_ROL" user_role
                           JOIN "SIGERSA"."ROL" role ON role.id = user_role.rol_id
                           WHERE user_role.usuario_id = user_account.id
                             AND user_role.activo = true
                             AND role.codigo IN ('ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO')))
                       OR EXISTS (
                           SELECT 1 FROM "SIGERSA"."USUARIO_DOCUMENTO_AUTORIZACION" document
                           WHERE document.usuario_id = user_account.id AND document.vigente = true)
                   ));
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
            return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                Sql(sql), new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<bool> PublicRegistrationExistsAsync(
        string normalizedEmail,
        string normalizedIdentification,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM "SIGERSA"."USUARIO"
                 WHERE correo_normalizado = @NormalizedEmail
                    OR identificacion_normalizada = @NormalizedIdentification);
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
            return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                Sql(sql), new { NormalizedEmail = normalizedEmail, NormalizedIdentification = normalizedIdentification },
                cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreatePublicRegistrationAsync(
        PublicUserRegistrationDraft draft,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."USUARIO"
                    (id, nombre_completo, tipo_identificacion, identificacion_normalizada,
                     correo, correo_normalizado, telefono, password_hash, estado, activo,
                     rol_solicitado, terminos_aceptados_en)
                VALUES
                    (@Id, @NombreCompleto, @TipoIdentificacion, @IdentificacionNormalizada,
                     @Correo, @Correo, @Telefono, @PasswordHash, 'PENDIENTE_VALIDACION', true,
                     @RequestedRole, @TermsAcceptedAt);

                INSERT INTO "SIGERSA"."USUARIO_DOCUMENTO_AUTORIZACION"
                    (id, usuario_id, bucket_name, supabase_path, nombre_original,
                     file_size, mime_type, hash, vigente)
                VALUES
                    (@DocumentId, @Id, @BucketName, @SupabasePath, @OriginalName,
                     @FileSize, @MimeType, @Sha256Hash, true);
                """), new
            {
                draft.Id,
                draft.NombreCompleto,
                draft.TipoIdentificacion,
                draft.IdentificacionNormalizada,
                draft.Correo,
                draft.Telefono,
                draft.PasswordHash,
                draft.RequestedRole,
                draft.TermsAcceptedAt,
                DocumentId = draft.AuthorizationLetter.Id,
                draft.AuthorizationLetter.BucketName,
                draft.AuthorizationLetter.SupabasePath,
                draft.AuthorizationLetter.OriginalName,
                draft.AuthorizationLetter.FileSize,
                draft.AuthorizationLetter.MimeType,
                draft.AuthorizationLetter.Sha256Hash
            }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return draft.Id;
        }
    }

    public async Task<Guid> CreateManagedAsync(
        ManagedUserDraft draft,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var roleId = await GetRoleIdAsync(connection, transaction, draft.Rol, cancellationToken);
            var userId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."USUARIO"
                    (id, empresa_id, nombre_completo, tipo_identificacion,
                     identificacion_normalizada, correo, correo_normalizado, telefono,
                     password_hash, estado, activo, creado_por)
                VALUES
                    (@UserId, @EmpresaId, @NombreCompleto, @TipoIdentificacion,
                     @IdentificacionNormalizada, @Correo, @Correo, @Telefono,
                     @PasswordHash, @Estado, true, @ActorId);
                """), new
            {
                UserId = userId,
                draft.EmpresaId,
                draft.NombreCompleto,
                draft.TipoIdentificacion,
                draft.IdentificacionNormalizada,
                draft.Correo,
                draft.Telefono,
                draft.PasswordHash,
                draft.Estado,
                ActorId = actorId
            }, transaction, cancellationToken: cancellationToken));
            await UpsertRoleAsync(connection, transaction, userId, roleId, draft, actorId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return userId;
        }
    }

    public async Task<bool> UpdateManagedAsync(
        Guid id,
        ManagedUserDraft draft,
        Guid actorId,
        Guid? companyScope,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var roleId = await GetRoleIdAsync(connection, transaction, draft.Rol, cancellationToken);
            var affected = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."USUARIO"
                   SET empresa_id = @EmpresaId, nombre_completo = @NombreCompleto,
                       tipo_identificacion = @TipoIdentificacion,
                       identificacion_normalizada = @IdentificacionNormalizada,
                       correo = @Correo, correo_normalizado = @Correo, telefono = @Telefono,
                       password_hash = COALESCE(@PasswordHash, password_hash),
                       rol_solicitado = @RequestedRole,
                       estado = @Estado, modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId, version_fila = version_fila + 1
                 WHERE id = @Id AND version_fila = @VersionFila
                   AND (@CompanyScope IS NULL OR empresa_id = @CompanyScope);
                """), new
            {
                Id = id,
                draft.EmpresaId,
                draft.NombreCompleto,
                draft.TipoIdentificacion,
                draft.IdentificacionNormalizada,
                draft.Correo,
                draft.Telefono,
                draft.PasswordHash,
                draft.Estado,
                draft.VersionFila,
                ActorId = actorId,
                CompanyScope = companyScope,
                RequestedRole = draft.Rol is "ADMINISTRADOR_EMPRESA" or "USUARIO_DELEGADO"
                    && draft.EmpresaId is null ? draft.Rol : null
            }, transaction, cancellationToken: cancellationToken));
            if (affected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."USUARIO_ROL"
                   SET activo = false, modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId, version_fila = version_fila + 1
                 WHERE usuario_id = @Id AND activo = true;
                """), new { Id = id, ActorId = actorId }, transaction, cancellationToken: cancellationToken));
            if (draft.Rol is not ("ADMINISTRADOR_EMPRESA" or "USUARIO_DELEGADO") || draft.EmpresaId is not null)
                await UpsertRoleAsync(connection, transaction, id, roleId, draft, actorId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }

    public async Task<bool> SetSuspendedAsync(
        Guid id,
        bool suspended,
        long versionFila,
        Guid actorId,
        Guid? companyScope,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                UPDATE "SIGERSA"."USUARIO"
                   SET estado = CASE WHEN @Suspended THEN 'SUSPENDIDO' ELSE 'ACTIVO' END,
                       fallos_acceso = CASE WHEN @Suspended THEN fallos_acceso ELSE 0 END,
                       bloqueado_hasta = NULL, modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId, version_fila = version_fila + 1
                 WHERE id = @Id AND version_fila = @VersionFila
                   AND (@CompanyScope IS NULL OR empresa_id = @CompanyScope);
                """), new
            {
                Id = id,
                Suspended = suspended,
                VersionFila = versionFila,
                ActorId = actorId,
                CompanyScope = companyScope
            }, cancellationToken: cancellationToken));
            return affected == 1;
        }
    }

    private static async Task<Guid> GetRoleIdAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string role,
        CancellationToken cancellationToken)
    {
        var roleId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(Sql("""
            SELECT id FROM "SIGERSA"."ROL" WHERE codigo = @Role AND activo = true;
            """), new { Role = role }, transaction, cancellationToken: cancellationToken));
        return roleId ?? throw new KeyNotFoundException("El rol seleccionado no existe o está inactivo.");
    }

    private static Task<int> UpsertRoleAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid userId,
        Guid roleId,
        ManagedUserDraft draft,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var companyRole = draft.Rol is "ADMINISTRADOR_EMPRESA" or "USUARIO_DELEGADO";
        return connection.ExecuteAsync(new CommandDefinition(Sql("""
            INSERT INTO "SIGERSA"."USUARIO_ROL"
                (id, usuario_id, rol_id, empresa_ambito_id, activo, creado_por)
            VALUES (@Id, @UserId, @RoleId, @CompanyScope, true, @ActorId)
            ON CONFLICT (usuario_id, rol_id, empresa_ambito_id, establecimiento_ambito_id)
            DO UPDATE SET activo = true, vigente_desde = CURRENT_TIMESTAMP,
                          vigente_hasta = NULL, modificado_en = CURRENT_TIMESTAMP,
                          modificado_por = @ActorId,
                          version_fila = "SIGERSA"."USUARIO_ROL".version_fila + 1;
            """), new
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            CompanyScope = companyRole ? draft.EmpresaId : null,
            ActorId = actorId
        }, transaction, cancellationToken: cancellationToken));
    }

    private sealed class UsuarioRow
    {
        public Guid Id { get; init; }
        public string Correo { get; init; } = string.Empty;
        public string NombreCompleto { get; init; } = string.Empty;
        public bool Activo { get; init; }
        public DateTime CreadoEn { get; init; }
        public Guid? CreadoPor { get; init; }
        public DateTime? ModificadoEn { get; init; }
        public Guid? ModificadoPor { get; init; }
        public long VersionFila { get; init; }

        public Usuario ToDomain() => new()
        {
            Id = Id,
            Correo = Correo,
            NombreCompleto = NombreCompleto,
            Activo = Activo,
            CreadoEn = Utc(CreadoEn),
            CreadoPor = CreadoPor,
            ModificadoEn = ModificadoEn is null ? null : Utc(ModificadoEn.Value),
            ModificadoPor = ModificadoPor,
            VersionFila = VersionFila
        };

        private static DateTimeOffset Utc(DateTime value) =>
            new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private sealed class ManagedUserRow
    {
        public Guid Id { get; init; }
        public string NombreCompleto { get; init; } = string.Empty;
        public string Correo { get; init; } = string.Empty;
        public string TipoIdentificacion { get; init; } = string.Empty;
        public string Identificacion { get; init; } = string.Empty;
        public string? Telefono { get; init; }
        public string[] Roles { get; init; } = [];
        public Guid? EmpresaId { get; init; }
        public string? EmpresaNombre { get; init; }
        public string Estado { get; init; } = string.Empty;
        public bool Activo { get; init; }
        public long VersionFila { get; init; }

        public ManagedUser ToDomain() => new(
            Id, NombreCompleto, Correo, TipoIdentificacion, Identificacion, Telefono,
            Roles, EmpresaId, EmpresaNombre, Estado, Activo, VersionFila);
    }

    private sealed class RoleOptionRow
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }

    private sealed class CompanyOptionRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
