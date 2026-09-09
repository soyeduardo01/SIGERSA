using SIGERSA.Domain.Exceptions;

namespace SIGERSA.Infrastructure.Persistence;

public static class OptimisticConcurrencyGuard
{
    public static void EnsureSingleRowUpdated(int affectedRows, Guid entityId)
    {
        if (affectedRows == 0) throw new OptimisticConcurrencyException(entityId);
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"La actualización de '{entityId}' afectó {affectedRows} filas; se esperaba exactamente una.");
        }
    }
}
