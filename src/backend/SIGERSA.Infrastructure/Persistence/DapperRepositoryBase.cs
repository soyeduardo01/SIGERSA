using Dapper;

namespace SIGERSA.Infrastructure.Persistence;

public abstract class DapperRepositoryBase(IDbConnectionFactory connectionFactory)
{
    protected IDbConnectionFactory ConnectionFactory { get; } = connectionFactory;

    protected static string Sql(string sql) => SigersaSqlGuard.EnsureQualified(sql);

    protected async Task ExecuteConcurrencyCheckedAsync(
        string sql,
        object parameters,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var command = new CommandDefinition(
                SigersaSqlGuard.EnsureAuditedConcurrencyUpdate(sql),
                parameters,
                cancellationToken: cancellationToken);

            var affectedRows = await connection.ExecuteAsync(command);
            OptimisticConcurrencyGuard.EnsureSingleRowUpdated(affectedRows, entityId);
        }
    }
}
