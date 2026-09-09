using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class AllItemsRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IAllItemsRepository
{
    private const string Projection = "\"Items\", \"ItemsId\", \"Description\", \"SectionType\", \"Parents\"";

    public async Task<IReadOnlyList<AllItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await QueryAsync<AllItem>($"SELECT {Projection} FROM \"SIGERSA\".\"AllItems\" ORDER BY \"Items\";", null, cancellationToken)).AsList();

    public async Task<AllItem?> GetByIdAsync(int items, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Projection} FROM \"SIGERSA\".\"AllItems\" WHERE \"Items\" = @Items;";
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.QuerySingleOrDefaultAsync<AllItem>(new CommandDefinition(Sql(sql), new { Items = items }, cancellationToken: cancellationToken));
        }
    }

    public async Task<AllItem> CreateAsync(AllItemDraft item, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            INSERT INTO "SIGERSA"."AllItems" ("ItemsId", "Description", "SectionType", "Parents")
            VALUES (@ItemsId, @Description, @SectionType, @Parents)
            RETURNING {Projection};
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.QuerySingleAsync<AllItem>(new CommandDefinition(Sql(sql), item, cancellationToken: cancellationToken));
        }
    }

    public async Task<bool> UpdateAsync(int items, AllItemDraft item, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."AllItems"
            SET "ItemsId" = @ItemsId, "Description" = @Description,
                "SectionType" = @SectionType, "Parents" = @Parents
            WHERE "Items" = @Items;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.ExecuteAsync(new CommandDefinition(
                Sql(sql), new { Items = items, item.ItemsId, item.Description, item.SectionType, item.Parents }, cancellationToken: cancellationToken)) == 1;
        }
    }

    public async Task<bool> DeleteAsync(int items, CancellationToken cancellationToken = default)
    {
        const string sql = """DELETE FROM "SIGERSA"."AllItems" WHERE "Items" = @Items;""";
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.ExecuteAsync(new CommandDefinition(Sql(sql), new { Items = items }, cancellationToken: cancellationToken)) == 1;
        }
    }

    private async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken cancellationToken)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.QueryAsync<T>(new CommandDefinition(Sql(sql), parameters, cancellationToken: cancellationToken));
        }
    }
}
