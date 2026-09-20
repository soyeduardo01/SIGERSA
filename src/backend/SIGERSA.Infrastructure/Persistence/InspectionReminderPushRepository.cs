using Dapper;
using SIGERSA.Domain.Notifications;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class InspectionReminderPushRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IInspectionReminderPushRepository
{
    public async Task<IReadOnlyList<InspectionReminderPushDispatch>> ClaimDueAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH seeded AS (
                INSERT INTO "SIGERSA"."DESPACHO_PUSH_RECORDATORIO" (notificacion_id)
                SELECT notification.id
                  FROM "SIGERSA"."NOTIFICACION" notification
                 WHERE notification.canal = 'INTERNA'
                   AND notification.tipo LIKE 'RECORDATORIO_INSPECCION_%'
                   AND notification.programada_para <= CURRENT_TIMESTAMP
                   AND notification.leida_en IS NULL
                ON CONFLICT (notificacion_id) DO NOTHING
                RETURNING notificacion_id
            ), candidates AS (
                SELECT dispatch.notificacion_id
                  FROM "SIGERSA"."DESPACHO_PUSH_RECORDATORIO" dispatch
                  JOIN "SIGERSA"."NOTIFICACION" notification
                    ON notification.id = dispatch.notificacion_id
                 WHERE notification.leida_en IS NULL
                   AND notification.programada_para <= CURRENT_TIMESTAMP
                   AND (
                       (dispatch.estado = 'PENDIENTE'
                        AND dispatch.proximo_intento_en <= CURRENT_TIMESTAMP)
                       OR
                       (dispatch.estado = 'EN_PROCESO'
                        AND dispatch.arrendado_hasta < CURRENT_TIMESTAMP)
                   )
                 ORDER BY notification.programada_para, notification.id
                 LIMIT @BatchSize
                 FOR UPDATE OF dispatch SKIP LOCKED
            ), claimed AS (
                UPDATE "SIGERSA"."DESPACHO_PUSH_RECORDATORIO" dispatch
                   SET estado = 'EN_PROCESO',
                       intentos = dispatch.intentos + 1,
                       arrendado_hasta = CURRENT_TIMESTAMP + @LeaseDuration,
                       ultimo_error = NULL,
                       modificado_en = CURRENT_TIMESTAMP
                  FROM candidates
                 WHERE dispatch.notificacion_id = candidates.notificacion_id
                RETURNING dispatch.notificacion_id, dispatch.intentos
            )
            SELECT notification.id AS NotificationId,
                   notification.usuario_id AS UserId,
                   notification.tipo AS Type,
                   notification.mensaje AS Message,
                   notification.recurso_id AS EvaluationId,
                   evaluation.numero AS EvaluationNumber,
                   establishment.nombre AS EstablishmentName,
                   notification.programada_para AS ScheduledFor,
                   claimed.intentos AS Attempt
              FROM claimed
              JOIN "SIGERSA"."NOTIFICACION" notification
                ON notification.id = claimed.notificacion_id
              JOIN "SIGERSA"."EVALUACION" evaluation
                ON evaluation.id = notification.recurso_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment
                ON establishment.id = evaluation.establecimiento_id
             ORDER BY notification.programada_para, notification.id;
            """;

        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DispatchRow>(new CommandDefinition(
            Sql(sql), new { BatchSize = batchSize, LeaseDuration = leaseDuration },
            cancellationToken: cancellationToken));
        return rows.Select(row => row.ToDomain()).ToArray();
    }

    public async Task MarkSentAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."DESPACHO_PUSH_RECORDATORIO"
               SET estado = 'ENVIADA', enviado_en = CURRENT_TIMESTAMP,
                   arrendado_hasta = NULL, ultimo_error = NULL,
                   modificado_en = CURRENT_TIMESTAMP
             WHERE notificacion_id = @NotificationId AND estado = 'EN_PROCESO';
            """;
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            Sql(sql), new { NotificationId = notificationId }, cancellationToken: cancellationToken));
    }

    public async Task MarkFailedAsync(
        Guid notificationId,
        string errorMessage,
        TimeSpan retryDelay,
        int maximumAttempts,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."DESPACHO_PUSH_RECORDATORIO"
               SET estado = CASE WHEN intentos >= @MaximumAttempts THEN 'AGOTADA' ELSE 'PENDIENTE' END,
                   proximo_intento_en = CURRENT_TIMESTAMP + @RetryDelay,
                   arrendado_hasta = NULL,
                   ultimo_error = left(@Error, 1000),
                   modificado_en = CURRENT_TIMESTAMP
             WHERE notificacion_id = @NotificationId AND estado = 'EN_PROCESO';
            """;
        await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql(sql), new
        {
            NotificationId = notificationId,
            Error = errorMessage,
            RetryDelay = retryDelay,
            MaximumAttempts = maximumAttempts
        }, cancellationToken: cancellationToken));
    }

    private sealed class DispatchRow
    {
        public Guid NotificationId { get; init; }
        public Guid UserId { get; init; }
        public string Type { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public Guid EvaluationId { get; init; }
        public string EvaluationNumber { get; init; } = string.Empty;
        public string EstablishmentName { get; init; } = string.Empty;
        public DateTime ScheduledFor { get; init; }
        public int Attempt { get; init; }

        public InspectionReminderPushDispatch ToDomain() => new(
            NotificationId, UserId, Type, Message, EvaluationId,
            EvaluationNumber, EstablishmentName,
            new DateTimeOffset(DateTime.SpecifyKind(ScheduledFor, DateTimeKind.Utc)), Attempt);
    }
}
