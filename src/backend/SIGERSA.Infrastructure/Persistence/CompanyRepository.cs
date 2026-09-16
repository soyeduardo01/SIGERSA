using Dapper;
using System.Text.Json;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class CompanyRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), ICompanyRepository
{
    public async Task<CompaniesPage> SearchAsync(
        CompanySearch search,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT company.id AS Id, company.razon_social AS LegalName,
                   company.rnc_normalizado AS TaxId,
                   company.nombre_comercial AS TradeName,
                   company.actividad_economica AS EconomicActivity,
                   company.telefono AS Phone, company.correo AS Email,
                   company.direccion AS Address,
                   company.municipio_id AS MunicipalityId, municipality.nombre AS MunicipalityName,
                   province.nombre AS ProvinceName,
                   COALESCE((
                       SELECT jsonb_agg(jsonb_build_object(
                           'Type', contact.tipo, 'FullName', contact.nombre_completo,
                           'Identification', contact.identificacion_normalizada,
                           'IdentificationType', contact.tipo_identificacion,
                           'Phone', contact.telefono, 'Email', contact.correo)
                           ORDER BY contact.tipo)::text
                         FROM "SIGERSA"."CONTACTO" contact
                        WHERE contact.empresa_id = company.id
                          AND contact.establecimiento_id IS NULL AND contact.activo = true
                   ), '[]') AS ContactsJson,
                   company.estado AS Status,
                   company.version_fila AS RowVersion
            FROM "SIGERSA"."EMPRESA" company
            LEFT JOIN "SIGERSA"."MUNICIPIO" municipality ON municipality.id = company.municipio_id
            LEFT JOIN "SIGERSA"."PROVINCIA" province ON province.id = municipality.provincia_id
            WHERE company.activo = true
              AND (@Search IS NULL OR lower(razon_social) LIKE '%' || @Search || '%'
                   OR lower(rnc_normalizado) LIKE '%' || @Search || '%'
                   OR lower(COALESCE(nombre_comercial, '')) LIKE '%' || @Search || '%')
              AND (@Status IS NULL OR estado = @Status)
            ORDER BY razon_social, id
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM "SIGERSA"."EMPRESA" company
            WHERE company.activo = true
              AND (@Search IS NULL OR lower(razon_social) LIKE '%' || @Search || '%'
                   OR lower(rnc_normalizado) LIKE '%' || @Search || '%'
                   OR lower(COALESCE(nombre_comercial, '')) LIKE '%' || @Search || '%')
              AND (@Status IS NULL OR estado = @Status);
            """;
        var parameters = new
        {
            search.Search,
            search.Status,
            Offset = (search.Page - 1) * search.PageSize,
            search.PageSize
        };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), parameters, cancellationToken: cancellationToken));
            var items = (await result.ReadAsync<CompanyRow>()).Select(row => row.ToDomain()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new CompaniesPage(items, search.Page, search.PageSize, total);
        }
    }

    public async Task<Guid> CreateAsync(
        CompanyDraft draft,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "SIGERSA"."EMPRESA"
                (id, razon_social, rnc_normalizado, nombre_comercial, actividad_economica,
                 telefono, correo, direccion, municipio_id, estado, representantes, activo, creado_por)
            VALUES
                (@Id, @LegalName, @TaxId, @TradeName, @EconomicActivity,
                 @Phone, @Email, @Address, @MunicipalityId, @Status, '[]'::jsonb, true, @ActorId);
            """;
        var id = Guid.NewGuid();
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await connection.ExecuteAsync(new CommandDefinition(Sql(sql), new
            {
                Id = id,
                draft.LegalName,
                draft.TaxId,
                draft.TradeName,
                draft.EconomicActivity,
                draft.Phone,
                draft.Email,
                draft.Address,
                draft.MunicipalityId,
                draft.Status,
                ActorId = actorId
            }, transaction, cancellationToken: cancellationToken));
            await ReplaceContactsAsync(connection, transaction, id, draft.Contacts, actorId, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."AUDITORIA_EVENTO"
                    (id, actor_id, accion, recurso_tipo, recurso_id, resultado,
                     valores_nuevos, metadatos, creado_por)
                SELECT gen_random_uuid(), @ActorId, 'CREAR_EMPRESA', 'EMPRESA', company.id,
                       'EXITOSO', to_jsonb(company), '{}'::jsonb, @ActorId
                  FROM "SIGERSA"."EMPRESA" company WHERE company.id = @Id;
                """), new { Id = id, ActorId = actorId }, transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }
        return id;
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        CompanyDraft draft,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."EMPRESA"
               SET razon_social = @LegalName, rnc_normalizado = @TaxId,
                   nombre_comercial = @TradeName, actividad_economica = @EconomicActivity,
                   telefono = @Phone, correo = @Email, direccion = @Address,
                   municipio_id = @MunicipalityId, estado = @Status,
                   modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                   version_fila = version_fila + 1
             WHERE id = @Id AND activo = true AND version_fila = @RowVersion;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var previous = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(Sql("""
                SELECT to_jsonb(company)::text FROM "SIGERSA"."EMPRESA" company
                 WHERE company.id = @Id AND company.activo = true AND company.version_fila = @RowVersion
                 FOR UPDATE;
                """), new { Id = id, draft.RowVersion }, transaction,
                cancellationToken: cancellationToken));
            var affected = await connection.ExecuteAsync(new CommandDefinition(Sql(sql), new
            {
                Id = id,
                draft.LegalName,
                draft.TaxId,
                draft.TradeName,
                draft.EconomicActivity,
                draft.Phone,
                draft.Email,
                draft.Address,
                draft.MunicipalityId,
                draft.Status,
                draft.RowVersion,
                ActorId = actorId
            }, transaction, cancellationToken: cancellationToken));
            if (affected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
            await ReplaceContactsAsync(connection, transaction, id, draft.Contacts, actorId, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."AUDITORIA_EVENTO"
                    (id, actor_id, accion, recurso_tipo, recurso_id, resultado,
                     valores_anteriores, valores_nuevos, metadatos, creado_por)
                SELECT gen_random_uuid(), @ActorId, 'ACTUALIZAR_EMPRESA', 'EMPRESA', company.id,
                       'EXITOSO', CAST(@Previous AS jsonb), to_jsonb(company), '{}'::jsonb, @ActorId
                  FROM "SIGERSA"."EMPRESA" company WHERE company.id = @Id;
                """), new { Id = id, ActorId = actorId, Previous = previous }, transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }

    private static async Task ReplaceContactsAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid companyId,
        IReadOnlyList<CompanyContact> contacts,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(Sql("""
            UPDATE "SIGERSA"."CONTACTO"
               SET activo = false, principal = false, modificado_por = @ActorId
             WHERE empresa_id = @CompanyId AND establecimiento_id IS NULL AND activo = true;
            """), new { CompanyId = companyId, ActorId = actorId }, transaction,
            cancellationToken: cancellationToken));

        foreach (var contact in contacts)
        {
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."CONTACTO"
                    (id, empresa_id, tipo, nombre_completo, identificacion_normalizada, tipo_identificacion,
                     telefono, correo, principal, activo, creado_por)
                VALUES (gen_random_uuid(), @CompanyId, @Type, @FullName, @Identification, @IdentificationType,
                        @Phone, @Email, @IsPrimary, true, @ActorId);
                """), new { CompanyId = companyId, contact.Type, contact.FullName,
                    contact.Identification, contact.IdentificationType, contact.Phone, contact.Email,
                    IsPrimary = contact.Type == "PRINCIPAL", ActorId = actorId }, transaction,
                cancellationToken: cancellationToken));
        }
    }

    private sealed class CompanyRow
    {
        public Guid Id { get; init; }
        public string LegalName { get; init; } = string.Empty;
        public string TaxId { get; init; } = string.Empty;
        public string? TradeName { get; init; }
        public string? EconomicActivity { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public string? Address { get; init; }
        public Guid? MunicipalityId { get; init; }
        public string? MunicipalityName { get; init; }
        public string? ProvinceName { get; init; }
        public string ContactsJson { get; init; } = "[]";
        public string Status { get; init; } = string.Empty;
        public long RowVersion { get; init; }

        public CompanySummary ToDomain() => new(
            Id, LegalName, TaxId, TradeName, EconomicActivity, Phone, Email, Address,
            MunicipalityId, MunicipalityName, ProvinceName,
            JsonSerializer.Deserialize<CompanyContact[]>(ContactsJson) ?? [], Status, RowVersion);
    }
}
