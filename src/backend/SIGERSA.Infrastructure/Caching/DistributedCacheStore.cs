using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using SIGERSA.Domain.Caching;

namespace SIGERSA.Infrastructure.Caching;

public sealed class DistributedCacheStore(IDistributedCache cache) : ICacheStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetAsync(key, cancellationToken);
        if (cached is not null)
        {
            var value = JsonSerializer.Deserialize<T>(cached, JsonOptions);
            if (value is not null) return value;
        }

        var created = await factory(cancellationToken);
        await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(created, JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = lifetime }, cancellationToken);
        return created;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(key, cancellationToken);
}
