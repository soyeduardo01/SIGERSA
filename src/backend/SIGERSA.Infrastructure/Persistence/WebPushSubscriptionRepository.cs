using Dapper;
using SIGERSA.Domain.Notifications;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class WebPushSubscriptionRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IWebPushSubscriptionRepository
{
    public async Task UpsertAsync(
        Guid userId,
        WebPushSubscriptionDraft subscription,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "SIGERSA"."SUSCRIPCION_PUSH"
                (id, usuario_id, endpoint, clave_p256dh, clave_auth, expira_en,
                 agente_usuario, activa, creado_por, modificado_por)
            VALUES
                (gen_random_uuid(), @UserId, @Endpoint, @P256dh, @Auth, @ExpiresAt,
                 @UserAgent, true, @UserId, @UserId)
            ON CONFLICT (endpoint) DO UPDATE
               SET usuario_id = EXCLUDED.usuario_id,
                   clave_p256dh = EXCLUDED.clave_p256dh,
                   clave_auth = EXCLUDED.clave_auth,
                   expira_en = EXCLUDED.expira_en,
                   agente_usuario = EXCLUDED.agente_usuario,
                   activa = true,
                   fallos_consecutivos = 0,
                   modificado_en = CURRENT_TIMESTAMP,
                   modificado_por = EXCLUDED.usuario_id,
                   version_fila = "SIGERSA"."SUSCRIPCION_PUSH".version_fila + 1;
            """;
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql(sql), new
        {
            UserId = userId,
            subscription.Endpoint,
            subscription.P256dh,
            subscription.Auth,
            subscription.ExpiresAt,
            subscription.UserAgent
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> RemoveAsync(
        Guid userId,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."SUSCRIPCION_PUSH"
               SET activa = false, modificado_en = CURRENT_TIMESTAMP, modificado_por = @UserId,
                   version_fila = version_fila + 1
             WHERE usuario_id = @UserId AND endpoint = @Endpoint AND activa = true;
            """;
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            Sql(sql), new { UserId = userId, Endpoint = endpoint }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<IReadOnlyList<WebPushSubscriptionData>> GetActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, usuario_id AS UserId, endpoint AS Endpoint,
                   clave_p256dh AS P256dh, clave_auth AS Auth, expira_en AS ExpiresAt
              FROM "SIGERSA"."SUSCRIPCION_PUSH"
             WHERE usuario_id = @UserId AND activa = true
               AND (expira_en IS NULL OR expira_en > CURRENT_TIMESTAMP)
             ORDER BY creado_en;
            """;
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<WebPushSubscriptionRow>(new CommandDefinition(
            Sql(sql), new { UserId = userId }, cancellationToken: cancellationToken));
        return rows.Select(row => row.ToDomain()).ToArray();
    }

    public async Task MarkSentAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."SUSCRIPCION_PUSH"
               SET ultimo_envio_en = CURRENT_TIMESTAMP, fallos_consecutivos = 0,
                   modificado_en = CURRENT_TIMESTAMP, version_fila = version_fila + 1
             WHERE id = @SubscriptionId;
            """;
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            Sql(sql), new { SubscriptionId = subscriptionId }, cancellationToken: cancellationToken));
    }

    public async Task MarkFailedAsync(
        Guid subscriptionId,
        bool deactivate,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."SUSCRIPCION_PUSH"
               SET ultimo_fallo_en = CURRENT_TIMESTAMP,
                   fallos_consecutivos = fallos_consecutivos + 1,
                   activa = CASE WHEN @Deactivate THEN false ELSE activa END,
                   modificado_en = CURRENT_TIMESTAMP, version_fila = version_fila + 1
             WHERE id = @SubscriptionId;
            """;
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            Sql(sql), new { SubscriptionId = subscriptionId, Deactivate = deactivate },
            cancellationToken: cancellationToken));
    }

    private sealed class WebPushSubscriptionRow
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public string Endpoint { get; init; } = string.Empty;
        public string P256dh { get; init; } = string.Empty;
        public string Auth { get; init; } = string.Empty;
        public DateTime? ExpiresAt { get; init; }

        public WebPushSubscriptionData ToDomain() => new(
            Id, UserId, Endpoint, P256dh, Auth,
            ExpiresAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(ExpiresAt.Value, DateTimeKind.Utc))
                : null);
    }
}
