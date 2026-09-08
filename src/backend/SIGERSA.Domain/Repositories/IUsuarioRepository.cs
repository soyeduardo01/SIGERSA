using SIGERSA.Domain.Entities;

namespace SIGERSA.Domain.Repositories;

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task ActualizarNombreAsync(
        Guid id,
        string nombreCompleto,
        Guid modificadoPor,
        long versionFila,
        CancellationToken cancellationToken = default);
}
