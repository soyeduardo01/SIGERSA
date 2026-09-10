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

    public async Task<bool> UpdateAsync(
        int items,
        AllItemDraft item,
        IReadOnlyCollection<int> childrenToReparent,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."AllItems"
            SET "ItemsId" = @ItemsId, "Description" = @Description,
                "SectionType" = @SectionType, "Parents" = @Parents
            WHERE "Items" = @Items;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var changed = await connection.ExecuteAsync(new CommandDefinition(
                Sql(sql), new { Items = items, item.ItemsId, item.Description, item.SectionType, item.Parents }, transaction, cancellationToken: cancellationToken)) == 1;
            if (!changed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            if (childrenToReparent.Count > 0)
            {
                var reparented = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                    UPDATE "SIGERSA"."AllItems"
                    SET "Parents" = @NewParentCode
                    WHERE "Items" = ANY(@ChildItems);
                    """), new { NewParentCode = item.ItemsId, ChildItems = childrenToReparent.ToArray() }, transaction, cancellationToken: cancellationToken));
                if (reparented != childrenToReparent.Count)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }

    public async Task<int> ApplyDeleteAsync(
        IReadOnlyCollection<int> itemsToDelete,
        IReadOnlyDictionary<int, string?> childrenToReparent,
        CancellationToken cancellationToken = default)
    {
        if (itemsToDelete.Count == 0) return 0;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            foreach (var child in childrenToReparent)
            {
                var updated = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                    UPDATE "SIGERSA"."AllItems"
                    SET "Parents" = @ParentCode
                    WHERE "Items" = @ChildItem;
                    """), new { ChildItem = child.Key, ParentCode = child.Value }, transaction, cancellationToken: cancellationToken));
                if (updated != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return 0;
                }
            }

            var deleted = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                DELETE FROM "SIGERSA"."AllItems"
                WHERE "Items" = ANY(@ItemsToDelete);
                """), new { ItemsToDelete = itemsToDelete.ToArray() }, transaction, cancellationToken: cancellationToken));
            if (deleted != itemsToDelete.Count)
            {
                await transaction.RollbackAsync(cancellationToken);
                return 0;
            }

            await transaction.CommitAsync(cancellationToken);
            return deleted;
        }
    }

    public async Task<IReadOnlyList<AllItem>> ReorderAsync(
        IReadOnlyList<int> orderedItems,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var existing = (await connection.QueryAsync<int>(new CommandDefinition(
                Sql("""SELECT "Items" FROM "SIGERSA"."AllItems" ORDER BY "Items" FOR UPDATE;"""),
                transaction: transaction,
                cancellationToken: cancellationToken))).AsList();
            if (orderedItems.Count != existing.Count ||
                orderedItems.Distinct().Count() != existing.Count ||
                !orderedItems.Order().SequenceEqual(existing))
            {
                throw new ArgumentException("El orden debe incluir cada nodo existente exactamente una vez.", nameof(orderedItems));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                Sql("""UPDATE "SIGERSA"."AllItems" SET "Items" = -"Items";"""),
                transaction: transaction,
                cancellationToken: cancellationToken));
            for (var index = 0; index < orderedItems.Count; index++)
            {
                await connection.ExecuteAsync(new CommandDefinition(Sql("""
                    UPDATE "SIGERSA"."AllItems"
                    SET "Items" = @NewItem
                    WHERE "Items" = -@OldItem;
                    """), new { OldItem = orderedItems[index], NewItem = index + 1 }, transaction, cancellationToken: cancellationToken));
            }
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                SELECT setval(
                    pg_get_serial_sequence('"SIGERSA"."AllItems"', 'Items'),
                    COALESCE((SELECT max("Items") FROM "SIGERSA"."AllItems"), 1),
                    EXISTS (SELECT 1 FROM "SIGERSA"."AllItems"));
                """), transaction: transaction, cancellationToken: cancellationToken));
            var reordered = (await connection.QueryAsync<AllItem>(new CommandDefinition(
                Sql($"SELECT {Projection} FROM \"SIGERSA\".\"AllItems\" ORDER BY \"Items\";"),
                transaction: transaction,
                cancellationToken: cancellationToken))).AsList();
            await transaction.CommitAsync(cancellationToken);
            return reordered;
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
