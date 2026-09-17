using Microsoft.Extensions.Caching.Memory;
using SIGERSA.Domain.Caching;

namespace SIGERSA.Infrastructure.Caching;

public sealed class InMemoryCacheStore(IMemoryCache cache) : ICacheStore
{
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var value = await cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = lifetime;
            return await factory(cancellationToken);
        });
        return value ?? throw new InvalidOperationException($"La fábrica de caché devolvió un valor nulo para '{key}'.");
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        cache.Remove(key);
        return Task.CompletedTask;
    }
}
