namespace SIGERSA.Domain.Exceptions;

public sealed class OptimisticConcurrencyException(Guid entityId)
    : Exception($"El registro '{entityId}' fue modificado por otro proceso.")
{
    public Guid EntityId { get; } = entityId;
}
