using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

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
}
