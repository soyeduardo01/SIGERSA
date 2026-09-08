namespace SIGERSA.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; init; }

    public DateTimeOffset CreadoEn { get; init; }

    public Guid? CreadoPor { get; init; }

    public DateTimeOffset? ModificadoEn { get; init; }

    public Guid? ModificadoPor { get; init; }

    public long VersionFila { get; init; }
}
