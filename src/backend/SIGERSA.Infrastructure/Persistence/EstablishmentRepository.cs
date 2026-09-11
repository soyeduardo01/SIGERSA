using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class EstablishmentRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IEstablishmentRepository
{
    public async Task<EstablishmentsPage> SearchAsync(
        EstablishmentSearch search,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT establishment.id AS Id, establishment.codigo AS Code, establishment.nombre AS Name,
                   establishment.empresa_id AS CompanyId, company.razon_social AS CompanyName,
                   province.nombre AS ProvinceName, municipality.nombre AS MunicipalityName,
                   establishment.telefono AS Phone, establishment.correo AS Email,
                   establishment.estado AS Status, establishment.version_fila AS RowVersion
            FROM "SIGERSA"."ESTABLECIMIENTO" AS establishment
            JOIN "SIGERSA"."EMPRESA" AS company ON company.id = establishment.empresa_id
            LEFT JOIN "SIGERSA"."MUNICIPIO" AS municipality ON municipality.id = establishment.municipio_id
            LEFT JOIN "SIGERSA"."PROVINCIA" AS province ON province.id = municipality.provincia_id
            WHERE establishment.activo = true
              AND (@Search IS NULL OR lower(establishment.nombre) LIKE '%' || @Search || '%'
                   OR lower(establishment.codigo) LIKE '%' || @Search || '%'
                   OR lower(company.razon_social) LIKE '%' || @Search || '%')
              AND (@Status IS NULL OR establishment.estado = @Status)
            ORDER BY establishment.nombre, establishment.id
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM "SIGERSA"."ESTABLECIMIENTO" AS establishment
            JOIN "SIGERSA"."EMPRESA" AS company ON company.id = establishment.empresa_id
            WHERE establishment.activo = true
              AND (@Search IS NULL OR lower(establishment.nombre) LIKE '%' || @Search || '%'
                   OR lower(establishment.codigo) LIKE '%' || @Search || '%'
                   OR lower(company.razon_social) LIKE '%' || @Search || '%')
              AND (@Status IS NULL OR establishment.estado = @Status);
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
            using var result = await connection.QueryMultipleAsync(
                new CommandDefinition(Sql(sql), parameters, cancellationToken: cancellationToken));
            var items = (await result.ReadAsync<EstablishmentSummary>()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new EstablishmentsPage(items, search.Page, search.PageSize, total);
        }
    }

    public async Task<EstablishmentDetails?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT establishment.empresa_id AS EmpresaId,
                   establishment.municipio_id AS MunicipioId,
                   establishment.dps_das_id AS DpsDasId,
                   establishment.comercializacion_id AS ComercializacionId,
                   establishment.codigo AS Codigo,
                   establishment.nombre AS Nombre,
                   establishment.calle AS Calle,
                   establishment.numero_direccion AS NumeroDireccion,
                   establishment.telefono AS Telefono,
                   establishment.correo AS Correo,
                   establishment.fecha_inicio_operaciones AS FechaInicioOperaciones,
                   establishment.permiso_sanitario_numero AS PermisoSanitarioNumero,
                   establishment.permiso_sanitario_vence_en AS PermisoSanitarioVenceEn,
                   establishment.productos_descripcion AS ProductosDescripcion,
                   establishment.produccion_anual AS ProduccionAnual,
                   establishment.empleados_mujeres AS EmpleadosMujeres,
                   establishment.empleados_hombres AS EmpleadosHombres,
                   establishment.rechazos_microbiologicos_ultimos_5_anios AS RechazosMicrobiologicosUltimos5Anios,
                   establishment.haccp_implementado AS HaccpImplementado,
                   establishment.nivel_haccp_porcentaje AS NivelHaccpPorcentaje,
                   establishment.plan_muestreo_microbiologico AS PlanMuestreoMicrobiologico,
                   establishment.aplicacion_muestreo_codigo AS AplicacionMuestreoCodigo,
                   establishment.es_suplidor_inabie AS EsSuplidorInabie,
                   establishment.distribucion_inabie_codigo AS DistribucionInabieCodigo,
                   establishment.estado AS Estado,
                   establishment.version_fila AS VersionFila,
                   company.razon_social AS CompanyName,
                   municipality.provincia_id AS ProvinceId
            FROM "SIGERSA"."ESTABLECIMIENTO" AS establishment
            JOIN "SIGERSA"."EMPRESA" AS company ON company.id = establishment.empresa_id
            LEFT JOIN "SIGERSA"."MUNICIPIO" AS municipality ON municipality.id = establishment.municipio_id
            WHERE establishment.id = @Id AND establishment.activo = true;

            SELECT tipo AS Type, nombre_completo AS FullName,
                   identificacion_normalizada AS Identification, telefono AS Phone, correo AS Email
            FROM "SIGERSA"."CONTACTO"
            WHERE establecimiento_id = @Id AND activo = true AND tipo IN ('PRINCIPAL', 'LEGAL')
            ORDER BY principal DESC, tipo;

            SELECT subcategoria_alimento_id AS SubcategoryId,
                   descripcion_producto AS Description, volumen_mensual AS MonthlyVolume,
                   unidad_medida AS Unit
            FROM "SIGERSA"."ESTABLECIMIENTO_PRODUCTO"
            WHERE establecimiento_id = @Id AND activo = true
            ORDER BY creado_en, id;

            SELECT mercado_objetivo_id
            FROM "SIGERSA"."ESTABLECIMIENTO_MERCADO"
            WHERE establecimiento_id = @Id AND activo = true;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(
                new CommandDefinition(Sql(sql), new { Id = id }, cancellationToken: cancellationToken));
            var row = await result.ReadSingleOrDefaultAsync<EstablishmentRow>();
            if (row is null) return null;
            var contacts = (await result.ReadAsync<EstablishmentContactDraft>()).ToArray();
            var products = (await result.ReadAsync<EstablishmentProductDraft>()).ToArray();
            var markets = (await result.ReadAsync<Guid>()).ToArray();
            return new EstablishmentDetails(id, row.ToDraft(markets, contacts, products), row.CompanyName, row.ProvinceId);
        }
    }

    public async Task<EstablishmentOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, rnc_normalizado AS Code, razon_social AS Name FROM "SIGERSA"."EMPRESA" WHERE activo = true ORDER BY razon_social;
            SELECT id AS Id, codigo AS Code, nombre AS Name FROM "SIGERSA"."PROVINCIA" WHERE activo = true ORDER BY nombre;
            SELECT id AS Id, provincia_id AS ProvinceId, codigo AS Code, nombre AS Name FROM "SIGERSA"."MUNICIPIO" WHERE activo = true ORDER BY nombre;
            SELECT id AS Id, codigo AS Code, nombre AS Name FROM "SIGERSA"."DPS_DAS" WHERE activo = true ORDER BY tipo, nombre;
            SELECT catalog.id AS Id, catalog.codigo AS Code, parameter."StringData" AS Name
            FROM "SIGERSA"."FN_ParametersControl_GetActive"('COMERCIALIZACION_ESTABLECIMIENTO', NULL) AS parameter
            JOIN "SIGERSA"."COMERCIALIZACION" AS catalog
              ON catalog.activo = true
             AND catalog.codigo = CASE parameter."CCode" WHEN 'INTERNAC' THEN 'INTERNACIONAL' ELSE parameter."CCode" END
            ORDER BY parameter."NumericData", parameter."ParametersId";
            SELECT catalog.id AS Id, catalog.codigo AS Code, parameter."StringData" AS Name
            FROM "SIGERSA"."FN_ParametersControl_GetActive"('MERCADO_OBJETIVO', NULL) AS parameter
            JOIN "SIGERSA"."MERCADO_OBJETIVO" AS catalog
              ON catalog.activo = true
             AND catalog.codigo = CASE parameter."CCode"
                 WHEN 'NINOS' THEN 'NINOS_MENORES'
                 WHEN 'EMBARAZ' THEN 'MUJERES_EMBARAZADAS'
                 WHEN 'MAYORES' THEN 'ADULTOS_MAYORES'
                 WHEN 'TODOS' THEN 'TODOS_SEGMENTOS'
                 ELSE parameter."CCode"
             END
            ORDER BY parameter."NumericData", parameter."ParametersId";
            SELECT id AS Id, codigo AS Code, nombre AS Name FROM "SIGERSA"."CATEGORIA_ALIMENTO" WHERE activo = true ORDER BY orden, nombre;
            SELECT id AS Id, categoria_alimento_id AS CategoryId, codigo AS Code,
                   nombre AS Name, nivel_riesgo::integer AS RiskLevel
            FROM "SIGERSA"."SUBCATEGORIA_ALIMENTO" WHERE activo = true ORDER BY orden, nombre;
            SELECT * FROM "SIGERSA"."FN_ParametersControl_GetActive"('ESTADO_ESTABLECIMIENTO', NULL);
            SELECT * FROM "SIGERSA"."FN_ParametersControl_GetActive"('NIVEL_IMPLEMENTACION_HACCP', NULL);
            SELECT * FROM "SIGERSA"."FN_ParametersControl_GetActive"('APLICACION_MUESTREO_MICROBIOLOGICO', NULL);
            SELECT * FROM "SIGERSA"."FN_ParametersControl_GetActive"('DISTRIBUCION_INABIE', NULL);
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(
                new CommandDefinition(Sql(sql), cancellationToken: cancellationToken));
            var companies = (await result.ReadAsync<EstablishmentOption>()).ToArray();
            var provinces = (await result.ReadAsync<EstablishmentOption>()).ToArray();
            var municipalities = (await result.ReadAsync<MunicipalityOption>()).ToArray();
            var dpsDas = (await result.ReadAsync<EstablishmentOption>()).ToArray();
            var commercializations = (await result.ReadAsync<EstablishmentOption>()).ToArray();
            var markets = (await result.ReadAsync<EstablishmentOption>()).ToArray();
            var categories = (await result.ReadAsync<EstablishmentOption>()).ToArray();
            var subcategories = (await result.ReadAsync<SubcategoryOption>()).ToArray();
            var statuses = (await result.ReadAsync<ParameterRow>()).Select(value => value.ToDomain()).ToArray();
            var haccpLevels = (await result.ReadAsync<ParameterRow>()).Select(value => value.ToDomain()).ToArray();
            var sampling = (await result.ReadAsync<ParameterRow>()).Select(value => value.ToDomain()).ToArray();
            var distributions = (await result.ReadAsync<ParameterRow>()).Select(value => value.ToDomain()).ToArray();
            return new EstablishmentOptions(companies, provinces, municipalities, dpsDas,
                commercializations, markets, categories, subcategories, statuses, haccpLevels,
                sampling, distributions);
        }
    }

    public async Task<Guid> CreateAsync(
        EstablishmentDraft draft,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var id = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(Sql(InsertSql),
                MainParameters(id, draft, actorId), transaction, cancellationToken: cancellationToken));
            await ReplaceRelatedAsync(connection, transaction, id, draft, actorId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return id;
        }
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        EstablishmentDraft draft,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(Sql(UpdateSql),
                MainParameters(id, draft, actorId), transaction, cancellationToken: cancellationToken));
            if (affected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
            await ReplaceRelatedAsync(connection, transaction, id, draft, actorId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }

    private static async Task ReplaceRelatedAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid establishmentId,
        EstablishmentDraft draft,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(Sql("""
            UPDATE "SIGERSA"."CONTACTO" SET activo = false, modificado_en = CURRENT_TIMESTAMP,
                modificado_por = @ActorId, version_fila = version_fila + 1
            WHERE establecimiento_id = @EstablishmentId AND activo = true AND tipo IN ('PRINCIPAL', 'LEGAL');
            UPDATE "SIGERSA"."ESTABLECIMIENTO_MERCADO" SET activo = false, modificado_en = CURRENT_TIMESTAMP,
                modificado_por = @ActorId, version_fila = version_fila + 1
            WHERE establecimiento_id = @EstablishmentId AND activo = true;
            UPDATE "SIGERSA"."ESTABLECIMIENTO_PRODUCTO" SET activo = false, modificado_en = CURRENT_TIMESTAMP,
                modificado_por = @ActorId, version_fila = version_fila + 1
            WHERE establecimiento_id = @EstablishmentId AND activo = true;
            """), new { EstablishmentId = establishmentId, ActorId = actorId }, transaction,
            cancellationToken: cancellationToken));

        foreach (var contact in draft.Contacts)
        {
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."CONTACTO"
                    (id, empresa_id, establecimiento_id, tipo, nombre_completo,
                     identificacion_normalizada, telefono, correo, principal, activo, creado_por)
                VALUES (gen_random_uuid(), @CompanyId, @EstablishmentId, @Type, @FullName,
                        @Identification, @Phone, @Email, @Principal, true, @ActorId);
                """), new
            {
                draft.CompanyId,
                EstablishmentId = establishmentId,
                contact.Type,
                contact.FullName,
                contact.Identification,
                contact.Phone,
                contact.Email,
                Principal = contact.Type == "PRINCIPAL",
                ActorId = actorId
            }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var marketId in draft.MarketIds.Distinct())
        {
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."ESTABLECIMIENTO_MERCADO"
                    (id, establecimiento_id, mercado_objetivo_id, activo, creado_por)
                VALUES (gen_random_uuid(), @EstablishmentId, @MarketId, true, @ActorId)
                ON CONFLICT (establecimiento_id, mercado_objetivo_id)
                DO UPDATE SET activo = true, modificado_en = CURRENT_TIMESTAMP,
                              modificado_por = @ActorId,
                              version_fila = "SIGERSA"."ESTABLECIMIENTO_MERCADO".version_fila + 1;
                """), new { EstablishmentId = establishmentId, MarketId = marketId, ActorId = actorId },
                transaction, cancellationToken: cancellationToken));
        }

        foreach (var product in draft.Products)
        {
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."ESTABLECIMIENTO_PRODUCTO"
                    (id, establecimiento_id, subcategoria_alimento_id, descripcion_producto,
                     volumen_mensual, unidad_medida, activo, creado_por)
                VALUES (gen_random_uuid(), @EstablishmentId, @SubcategoryId, @Description,
                        @MonthlyVolume, @Unit, true, @ActorId)
                ON CONFLICT (establecimiento_id, subcategoria_alimento_id, descripcion_producto)
                DO UPDATE SET volumen_mensual = EXCLUDED.volumen_mensual,
                              unidad_medida = EXCLUDED.unidad_medida, activo = true,
                              modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                              version_fila = "SIGERSA"."ESTABLECIMIENTO_PRODUCTO".version_fila + 1;
                """), new
            {
                EstablishmentId = establishmentId,
                product.SubcategoryId,
                product.Description,
                product.MonthlyVolume,
                product.Unit,
                ActorId = actorId
            }, transaction, cancellationToken: cancellationToken));
        }
    }

    private static object MainParameters(Guid id, EstablishmentDraft draft, Guid actorId) => new
    {
        Id = id,
        draft.CompanyId,
        draft.MunicipalityId,
        draft.DpsDasId,
        draft.CommercializationId,
        draft.Name,
        draft.Street,
        draft.AddressNumber,
        draft.Phone,
        draft.Email,
        draft.OperationsStartDate,
        draft.SanitaryPermitNumber,
        draft.SanitaryPermitExpiresAt,
        draft.ProductsDescription,
        draft.AnnualProduction,
        draft.FemaleEmployees,
        draft.MaleEmployees,
        draft.MicrobiologicalRejectionsLastFiveYears,
        draft.HaccpImplemented,
        draft.HaccpPercentage,
        draft.MicrobiologicalSamplingPlan,
        draft.SamplingApplicationCode,
        draft.IsInabieSupplier,
        draft.InabieDistributionCode,
        draft.Status,
        draft.RowVersion,
        ActorId = actorId
    };

    private const string InsertSql = """
        INSERT INTO "SIGERSA"."ESTABLECIMIENTO"
            (id, empresa_id, municipio_id, dps_das_id, comercializacion_id, nombre,
             calle, numero_direccion, telefono, correo, fecha_inicio_operaciones,
             permiso_sanitario_numero, permiso_sanitario_vence_en, productos_descripcion,
             produccion_anual, empleados_mujeres, empleados_hombres,
             rechazos_microbiologicos_ultimos_5_anios, haccp_implementado,
             nivel_haccp_porcentaje, plan_muestreo_microbiologico, aplicacion_muestreo_codigo,
             es_suplidor_inabie, distribucion_inabie_codigo, estado, activo, creado_por)
        VALUES
            (@Id, @CompanyId, @MunicipalityId, @DpsDasId, @CommercializationId, @Name,
             @Street, @AddressNumber, @Phone, @Email, @OperationsStartDate,
             @SanitaryPermitNumber, @SanitaryPermitExpiresAt, @ProductsDescription,
             @AnnualProduction, @FemaleEmployees, @MaleEmployees,
             @MicrobiologicalRejectionsLastFiveYears, @HaccpImplemented,
             @HaccpPercentage, @MicrobiologicalSamplingPlan, @SamplingApplicationCode,
             @IsInabieSupplier, @InabieDistributionCode, @Status, true, @ActorId);
        """;

    private const string UpdateSql = """
        UPDATE "SIGERSA"."ESTABLECIMIENTO"
           SET municipio_id = @MunicipalityId, dps_das_id = @DpsDasId,
               comercializacion_id = @CommercializationId, nombre = @Name,
               calle = @Street, numero_direccion = @AddressNumber, telefono = @Phone, correo = @Email,
               fecha_inicio_operaciones = @OperationsStartDate,
               permiso_sanitario_numero = @SanitaryPermitNumber,
               permiso_sanitario_vence_en = @SanitaryPermitExpiresAt,
               productos_descripcion = @ProductsDescription, produccion_anual = @AnnualProduction,
               empleados_mujeres = @FemaleEmployees, empleados_hombres = @MaleEmployees,
               rechazos_microbiologicos_ultimos_5_anios = @MicrobiologicalRejectionsLastFiveYears,
               haccp_implementado = @HaccpImplemented, nivel_haccp_porcentaje = @HaccpPercentage,
               plan_muestreo_microbiologico = @MicrobiologicalSamplingPlan,
               aplicacion_muestreo_codigo = @SamplingApplicationCode,
               es_suplidor_inabie = @IsInabieSupplier,
               distribucion_inabie_codigo = @InabieDistributionCode, estado = @Status,
               modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
               version_fila = version_fila + 1
         WHERE id = @Id AND empresa_id = @CompanyId AND activo = true AND version_fila = @RowVersion;
        """;

    private sealed class EstablishmentRow
    {
        public Guid EmpresaId { get; init; }
        public Guid? MunicipioId { get; init; }
        public Guid? DpsDasId { get; init; }
        public Guid? ComercializacionId { get; init; }
        public string Codigo { get; init; } = string.Empty;
        public string Nombre { get; init; } = string.Empty;
        public string? Calle { get; init; }
        public string? NumeroDireccion { get; init; }
        public string? Telefono { get; init; }
        public string? Correo { get; init; }
        public DateTimeOffset? FechaInicioOperaciones { get; init; }
        public string? PermisoSanitarioNumero { get; init; }
        public DateTimeOffset? PermisoSanitarioVenceEn { get; init; }
        public string? ProductosDescripcion { get; init; }
        public decimal? ProduccionAnual { get; init; }
        public int? EmpleadosMujeres { get; init; }
        public int? EmpleadosHombres { get; init; }
        public int RechazosMicrobiologicosUltimos5Anios { get; init; }
        public bool? HaccpImplementado { get; init; }
        public decimal? NivelHaccpPorcentaje { get; init; }
        public bool? PlanMuestreoMicrobiologico { get; init; }
        public string? AplicacionMuestreoCodigo { get; init; }
        public bool? EsSuplidorInabie { get; init; }
        public string? DistribucionInabieCodigo { get; init; }
        public string Estado { get; init; } = string.Empty;
        public long VersionFila { get; init; }
        public string CompanyName { get; init; } = string.Empty;
        public Guid? ProvinceId { get; init; }

        public EstablishmentDraft ToDraft(
            Guid[] markets,
            EstablishmentContactDraft[] contacts,
            EstablishmentProductDraft[] products) => new(
            EmpresaId, MunicipioId, DpsDasId, ComercializacionId, Codigo, Nombre, Calle,
            NumeroDireccion, Telefono, Correo, FechaInicioOperaciones, PermisoSanitarioNumero,
            PermisoSanitarioVenceEn, ProductosDescripcion, ProduccionAnual, EmpleadosMujeres,
            EmpleadosHombres, RechazosMicrobiologicosUltimos5Anios,
            HaccpImplementado, NivelHaccpPorcentaje,
            PlanMuestreoMicrobiologico, AplicacionMuestreoCodigo, EsSuplidorInabie,
            DistribucionInabieCodigo, Estado, markets, contacts, products, VersionFila);
    }

    private sealed class ParameterRow
    {
        public long ParametersId { get; init; }
        public string KeyWord { get; init; } = string.Empty;
        public int? CompanyCode { get; init; }
        public int? OCode { get; init; }
        public string? CCode { get; init; }
        public int? NumericData { get; init; }
        public double? DoubleData { get; init; }
        public string? StringData { get; init; }
        public bool? BooleanData { get; init; }
        public DateTime? DateData { get; init; }
        public bool Status { get; init; }
        public string CUser { get; init; } = string.Empty;
        public DateTime CDate { get; init; }
        public string? MUser { get; init; }
        public DateTime? MDate { get; init; }
        public string? DUser { get; init; }
        public DateTime? DDate { get; init; }

        public ParameterControl ToDomain() => new(
            ParametersId, KeyWord, CompanyCode, OCode, CCode, NumericData, DoubleData,
            StringData, BooleanData, Utc(DateData), Status, CUser, Utc(CDate), MUser,
            Utc(MDate), DUser, Utc(DDate));

        private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
        private static DateTimeOffset? Utc(DateTime? value) => value is null ? null : Utc(value.Value);
    }
}
