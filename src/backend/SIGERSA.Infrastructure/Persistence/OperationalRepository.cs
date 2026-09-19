using Dapper;
using System.Text.Json;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class OperationalRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IOperationalRepository
{
    private const string CaseAccess = """
        (@GlobalScope = true
         OR (@CompanyId IS NOT NULL AND inspection_case.empresa_id = @CompanyId
             AND (@OwnerOnly = false OR EXISTS (
                 SELECT 1 FROM "SIGERSA"."SOLICITUD" owner_request
                  WHERE owner_request.id = inspection_case.solicitud_id
                    AND owner_request.solicitante_id = @UserId)))
         OR (@AssignedOnly = true AND EXISTS (
             SELECT 1 FROM "SIGERSA"."EVALUACION" scoped_evaluation
              WHERE scoped_evaluation.caso_id = inspection_case.id
                AND scoped_evaluation.evaluador_principal_id = @UserId)))
        """;

    private const string EvaluationAccess = """
        (@GlobalScope = true
         OR (@CompanyId IS NOT NULL AND inspection_case.empresa_id = @CompanyId
             AND (@OwnerOnly = false OR EXISTS (
                 SELECT 1 FROM "SIGERSA"."SOLICITUD" owner_request
                  WHERE owner_request.id = inspection_case.solicitud_id
                    AND owner_request.solicitante_id = @UserId)))
         OR (@AssignedOnly = true AND evaluation.evaluador_principal_id = @UserId))
        """;

    public async Task<DashboardSnapshot> GetDashboardAsync(
        OperationalActorScope scope,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT COUNT(*) FROM "SIGERSA"."CASO" inspection_case
             WHERE inspection_case.estado <> 'CERRADO' AND {CaseAccess};

            SELECT COUNT(*)
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
             WHERE evaluation.estado NOT IN ('APROBADA', 'CERRADA') AND {EvaluationAccess};

            SELECT COUNT(*)
              FROM "SIGERSA"."ALERTA_LAPCH" alert
              LEFT JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = alert.establecimiento_id
             WHERE (@GlobalScope = true
                    OR (@CompanyId IS NOT NULL AND COALESCE(alert.empresa_id, establishment.empresa_id) = @CompanyId)
                    OR (@AssignedOnly = true AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."CASO" inspection_case
                        JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.caso_id = inspection_case.id
                        WHERE inspection_case.alerta_lapch_id = alert.id
                          AND evaluation.evaluador_principal_id = @UserId)));

            SELECT round(avg(evaluation.porcentaje_cumplimiento), 2)
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
             WHERE evaluation.porcentaje_cumplimiento IS NOT NULL AND {EvaluationAccess};

            SELECT COUNT(*)
              FROM "SIGERSA"."SOLICITUD" request
             WHERE request.activo = true
               AND (@GlobalScope = true OR request.empresa_id = @CompanyId OR request.solicitante_id = @UserId);

            SELECT COUNT(*)
             FROM "SIGERSA"."NOTIFICACION" notification
             WHERE notification.usuario_id = @UserId
               AND notification.canal = 'INTERNA' AND notification.leida_en IS NULL
               AND notification.programada_para <= CURRENT_TIMESTAMP;

            SELECT COUNT(*)
              FROM "SIGERSA"."PROGRAMACION" schedule
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = schedule.caso_id
             WHERE schedule.estado IN ('PROGRAMADA', 'REPROGRAMADA') AND {CaseAccess};

            SELECT COUNT(*)
              FROM "SIGERSA"."DENUNCIA" complaint
              LEFT JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = complaint.establecimiento_id
             WHERE (@GlobalScope = true
                    OR (@CompanyId IS NOT NULL AND establishment.empresa_id = @CompanyId)
                    OR (@AssignedOnly = true AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."CASO" inspection_case
                        JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.caso_id = inspection_case.id
                        WHERE inspection_case.denuncia_id = complaint.id
                          AND evaluation.evaluador_principal_id = @UserId)));

            SELECT COUNT(*)
              FROM "SIGERSA"."PROGRAMACION" schedule
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = schedule.caso_id
             WHERE schedule.estado IN ('PROGRAMADA', 'REPROGRAMADA')
               AND NOT EXISTS (SELECT 1 FROM "SIGERSA"."ASIGNACION" assignment
                                WHERE assignment.programacion_id = schedule.id AND assignment.estado = 'ACTIVA')
               AND {CaseAccess};

            SELECT COUNT(*)
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
             WHERE evaluation.estado IN ('FINALIZADA', 'ENVIADA', 'EN_REVISION', 'APROBADA')
               AND NOT EXISTS (SELECT 1 FROM "SIGERSA"."INFORME" report
                                WHERE report.evaluacion_id = evaluation.id AND report.estado = 'EMITIDO')
               AND {EvaluationAccess};

            SELECT evaluation.id AS Id, evaluation.numero AS Number,
                   establishment.nombre AS EstablishmentName, evaluation.estado AS Status,
                   evaluation.porcentaje_cumplimiento AS CompliancePercentage,
                   evaluation.nivel_riesgo AS RiskLevel, evaluation.modificado_en AS UpdatedAt
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
             WHERE {EvaluationAccess}
             ORDER BY evaluation.modificado_en DESC, evaluation.id
             LIMIT 6;

            SELECT COALESCE(evaluation.nivel_riesgo, 'NO_CALCULABLE') AS Level, COUNT(*)::integer AS Total
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
             WHERE {EvaluationAccess}
             GROUP BY COALESCE(evaluation.nivel_riesgo, 'NO_CALCULABLE')
             ORDER BY Level;

            SELECT schedule.id AS Id, inspection_case.numero AS CaseNumber,
                   establishment.nombre AS EstablishmentName,
                   schedule.inicio_programado AS StartsAt, schedule.estado AS Status
              FROM "SIGERSA"."PROGRAMACION" schedule
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = schedule.caso_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = inspection_case.establecimiento_id
             WHERE schedule.estado IN ('PROGRAMADA', 'REPROGRAMADA')
               AND schedule.inicio_programado >= CURRENT_TIMESTAMP AND {CaseAccess}
             ORDER BY schedule.inicio_programado
             LIMIT 6;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), ScopeParameters(scope), cancellationToken: cancellationToken));
            var activeCases = await result.ReadSingleAsync<int>();
            var pendingEvaluations = await result.ReadSingleAsync<int>();
            var criticalAlerts = await result.ReadSingleAsync<int>();
            var averageCompliance = await result.ReadSingleOrDefaultAsync<decimal?>();
            var myRequests = await result.ReadSingleAsync<int>();
            var unreadNotifications = await result.ReadSingleAsync<int>();
            var scheduledEvaluations = await result.ReadSingleAsync<int>();
            var openComplaints = await result.ReadSingleAsync<int>();
            var pendingAssignments = await result.ReadSingleAsync<int>();
            var pendingReports = await result.ReadSingleAsync<int>();
            var evaluations = (await result.ReadAsync<DashboardEvaluationRow>()).Select(row => row.ToDomain()).ToArray();
            var risks = (await result.ReadAsync<DashboardRiskSlice>()).ToArray();
            var schedules = (await result.ReadAsync<DashboardScheduleRow>()).Select(row => row.ToDomain()).ToArray();
            return new DashboardSnapshot(
                activeCases, pendingEvaluations, criticalAlerts, averageCompliance,
                myRequests, unreadNotifications, scheduledEvaluations, openComplaints,
                pendingAssignments, pendingReports, evaluations, risks, schedules);
        }
    }

    public async Task<IReadOnlyList<NotificationRecord>> GetNotificationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, tipo AS Type, titulo AS Title, mensaje AS Message,
                   recurso_tipo AS ResourceType, recurso_id AS ResourceId,
                   programada_para AS ScheduledFor, creado_en AS CreatedAt, leida_en AS ReadAt
              FROM "SIGERSA"."NOTIFICACION"
             WHERE usuario_id = @UserId AND canal = 'INTERNA'
               AND programada_para <= CURRENT_TIMESTAMP
             ORDER BY (leida_en IS NULL) DESC, programada_para DESC, id
             LIMIT 100;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var rows = await connection.QueryAsync<NotificationRow>(new CommandDefinition(
                Sql(sql), new { UserId = userId }, cancellationToken: cancellationToken));
            return rows.Select(row => row.ToDomain()).ToArray();
        }
    }

    public async Task<bool> MarkNotificationReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."NOTIFICACION"
               SET estado = 'LEIDA', leida_en = COALESCE(leida_en, CURRENT_TIMESTAMP),
                   modificado_en = CURRENT_TIMESTAMP, modificado_por = @UserId,
                   version_fila = version_fila + 1
             WHERE id = @NotificationId AND usuario_id = @UserId
               AND canal = 'INTERNA' AND programada_para <= CURRENT_TIMESTAMP;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.ExecuteAsync(new CommandDefinition(
                Sql(sql), new { NotificationId = notificationId, UserId = userId },
                cancellationToken: cancellationToken)) == 1;
        }
    }

    public async Task<SurveillancePage> SearchSurveillanceAsync(
        SurveillanceSearch search,
        CancellationToken cancellationToken = default)
    {
        const string records = """
            SELECT alert.id, 'ALERTA_LAPCH'::varchar AS Kind, alert.numero_alerta AS Number,
                   alert.fecha_alerta AS OccurredAt,
                   COALESCE(alert.empresa_id, establishment.empresa_id) AS CompanyId,
                   company.razon_social AS CompanyName,
                   alert.establecimiento_id AS EstablishmentId, establishment.nombre AS EstablishmentName,
                   alert.producto AS Subject, alert.descripcion AS Description,
                   alert.prioridad AS Priority, NULL::varchar AS Channel,
                   false AS IsAnonymous, false AS IsConfidential, alert.resultado AS Result,
                   EXISTS (SELECT 1 FROM "SIGERSA"."CASO" inspection_case WHERE inspection_case.alerta_lapch_id = alert.id) AS HasCase,
                   alert.version_fila AS RowVersion
              FROM "SIGERSA"."ALERTA_LAPCH" alert
              LEFT JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = alert.establecimiento_id
              LEFT JOIN "SIGERSA"."EMPRESA" company ON company.id = COALESCE(alert.empresa_id, establishment.empresa_id)
             WHERE (@GlobalScope = true
                    OR (@CompanyId IS NOT NULL AND COALESCE(alert.empresa_id, establishment.empresa_id) = @CompanyId)
                    OR (@AssignedOnly = true AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."CASO" inspection_case
                        JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.caso_id = inspection_case.id
                        WHERE inspection_case.alerta_lapch_id = alert.id AND evaluation.evaluador_principal_id = @UserId)))
            UNION ALL
            SELECT complaint.id, 'DENUNCIA'::varchar AS Kind, complaint.numero AS Number,
                   complaint.fecha_recepcion AS OccurredAt, establishment.empresa_id AS CompanyId,
                   company.razon_social AS CompanyName,
                   complaint.establecimiento_id AS EstablishmentId, establishment.nombre AS EstablishmentName,
                   complaint.tipo_denuncia AS Subject, complaint.descripcion AS Description,
                   3::smallint AS Priority, complaint.canal AS Channel,
                   complaint.es_anonima AS IsAnonymous, complaint.es_confidencial AS IsConfidential,
                   complaint.resultado AS Result,
                   EXISTS (SELECT 1 FROM "SIGERSA"."CASO" inspection_case WHERE inspection_case.denuncia_id = complaint.id) AS HasCase,
                   complaint.version_fila AS RowVersion
              FROM "SIGERSA"."DENUNCIA" complaint
              LEFT JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = complaint.establecimiento_id
              LEFT JOIN "SIGERSA"."EMPRESA" company ON company.id = establishment.empresa_id
             WHERE (@GlobalScope = true
                    OR (@CompanyId IS NOT NULL AND establishment.empresa_id = @CompanyId)
                    OR (@AssignedOnly = true AND EXISTS (
                        SELECT 1 FROM "SIGERSA"."CASO" inspection_case
                        JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.caso_id = inspection_case.id
                        WHERE inspection_case.denuncia_id = complaint.id AND evaluation.evaluador_principal_id = @UserId)))
            """;
        var sql = $"""
            WITH records AS ({records})
            SELECT * FROM records
             WHERE (@Kind IS NULL OR Kind = @Kind)
               AND (@Result IS NULL OR Result = @Result)
               AND (@Search IS NULL OR lower(Number) LIKE '%' || @Search || '%'
                    OR lower(Subject) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(CompanyName, '')) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(EstablishmentName, '')) LIKE '%' || @Search || '%')
             ORDER BY OccurredAt DESC, Id
             OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            WITH records AS ({records})
            SELECT COUNT(*) FROM records
             WHERE (@Kind IS NULL OR Kind = @Kind)
               AND (@Result IS NULL OR Result = @Result)
               AND (@Search IS NULL OR lower(Number) LIKE '%' || @Search || '%'
                    OR lower(Subject) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(CompanyName, '')) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(EstablishmentName, '')) LIKE '%' || @Search || '%');
            """;
        var parameters = new
        {
            search.Search,
            search.Kind,
            search.Result,
            Offset = (search.Page - 1) * search.PageSize,
            search.PageSize,
            search.Scope.UserId,
            search.Scope.CompanyId,
            search.Scope.GlobalScope,
            search.Scope.AssignedOnly
        };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), parameters, cancellationToken: cancellationToken));
            var items = (await result.ReadAsync<SurveillanceRow>()).Select(row => row.ToDomain()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new SurveillancePage(items, search.Page, search.PageSize, total);
        }
    }

    public async Task<SurveillanceOptions> GetSurveillanceOptionsAsync(
        bool canManage,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, razon_social AS Name, id AS CompanyId
              FROM "SIGERSA"."EMPRESA"
             WHERE activo = true AND estado = 'ACTIVA' AND @CanManage = true
             ORDER BY razon_social;

            SELECT establishment.id AS Id,
                   establishment.nombre || ' · ' || company.razon_social AS Name,
                   establishment.empresa_id AS CompanyId
              FROM "SIGERSA"."ESTABLECIMIENTO" establishment
              JOIN "SIGERSA"."EMPRESA" company ON company.id = establishment.empresa_id
             WHERE establishment.activo = true AND @CanManage = true
             ORDER BY establishment.nombre;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), new { CanManage = canManage }, cancellationToken: cancellationToken));
            var companies = (await result.ReadAsync<OperationalOption>()).ToArray();
            var establishments = (await result.ReadAsync<OperationalOption>()).ToArray();
            return new SurveillanceOptions(companies, establishments, canManage);
        }
    }

    public async Task<Guid> CreateSurveillanceAsync(
        SurveillanceDraft draft,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            if (draft.Kind == "ALERTA_LAPCH")
            {
                var affected = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                    INSERT INTO "SIGERSA"."ALERTA_LAPCH"
                        (id, numero_alerta, fecha_alerta, empresa_id, establecimiento_id,
                         producto, descripcion, prioridad, resultado, registrada_por, creado_por)
                    SELECT @Id, 'LAPCH-' || to_char(CURRENT_TIMESTAMP, 'YYYYMMDD') || '-' || upper(substr(replace(@Id::text, '-', ''), 1, 8)),
                           @OccurredAt, COALESCE(@CompanyId, establishment.empresa_id), @EstablishmentId,
                           @Subject, @Description, @Priority, @Result, @ActorId, @ActorId
                      FROM (SELECT 1) seed
                      LEFT JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = @EstablishmentId
                     WHERE (@CompanyId IS NOT NULL OR establishment.id IS NOT NULL)
                       AND (@CompanyId IS NULL OR establishment.id IS NULL OR establishment.empresa_id = @CompanyId);
                    """), new { Id = id, draft.OccurredAt, draft.CompanyId, draft.EstablishmentId, draft.Subject, draft.Description, draft.Priority, draft.Result, ActorId = actorId }, cancellationToken: cancellationToken));
                if (affected != 1) throw new ArgumentException("La empresa o el establecimiento de la alerta no es válido.");
            }
            else
            {
                var affected = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                    INSERT INTO "SIGERSA"."DENUNCIA"
                        (id, numero, establecimiento_id, tipo_denuncia, fecha_recepcion, canal,
                         es_anonima, es_confidencial, descripcion, resultado, registrada_por, creado_por)
                    SELECT @Id, 'DEN-' || to_char(CURRENT_TIMESTAMP, 'YYYYMMDD') || '-' || upper(substr(replace(@Id::text, '-', ''), 1, 8)),
                           establishment.id, @Subject, @OccurredAt, @Channel, @IsAnonymous,
                           @IsConfidential, @Description, @Result, @ActorId, @ActorId
                      FROM "SIGERSA"."ESTABLECIMIENTO" establishment
                     WHERE establishment.id = @EstablishmentId AND establishment.activo = true;
                    """), new { Id = id, draft.EstablishmentId, draft.Subject, draft.OccurredAt, draft.Channel, draft.IsAnonymous, draft.IsConfidential, draft.Description, draft.Result, ActorId = actorId }, cancellationToken: cancellationToken));
                if (affected != 1) throw new ArgumentException("El establecimiento de la denuncia no es válido.");
            }
        }
        return id;
    }

    public async Task<bool> UpdateSurveillanceAsync(
        Guid id,
        SurveillanceDraft draft,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var sql = draft.Kind == "ALERTA_LAPCH"
            ? """
                UPDATE "SIGERSA"."ALERTA_LAPCH" alert
                   SET fecha_alerta = @OccurredAt,
                       empresa_id = COALESCE(
                           @CompanyId,
                           (SELECT empresa_id FROM "SIGERSA"."ESTABLECIMIENTO" WHERE id = @EstablishmentId)),
                       establecimiento_id = @EstablishmentId, producto = @Subject,
                       descripcion = @Description, prioridad = @Priority, resultado = @Result,
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE alert.id = @Id AND alert.version_fila = @RowVersion
                   AND (@EstablishmentId IS NULL OR EXISTS (
                       SELECT 1
                         FROM "SIGERSA"."ESTABLECIMIENTO" establishment
                        WHERE establishment.id = @EstablishmentId
                          AND establishment.activo = true
                          AND (@CompanyId IS NULL OR establishment.empresa_id = @CompanyId)));
                """
            : """
                UPDATE "SIGERSA"."DENUNCIA"
                   SET establecimiento_id = @EstablishmentId, tipo_denuncia = @Subject,
                       fecha_recepcion = @OccurredAt, canal = @Channel,
                       es_anonima = @IsAnonymous, es_confidencial = @IsConfidential,
                       descripcion = @Description, resultado = @Result,
                       modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                       version_fila = version_fila + 1
                 WHERE id = @Id AND version_fila = @RowVersion
                   AND EXISTS (SELECT 1 FROM "SIGERSA"."ESTABLECIMIENTO" WHERE id = @EstablishmentId AND activo = true);
                """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(Sql(sql), new
            {
                Id = id,
                draft.OccurredAt,
                draft.CompanyId,
                draft.EstablishmentId,
                draft.Subject,
                draft.Description,
                draft.Priority,
                draft.Channel,
                draft.IsAnonymous,
                draft.IsConfidential,
                draft.Result,
                draft.RowVersion,
                ActorId = actorId
            }, cancellationToken: cancellationToken));
            return affected == 1;
        }
    }

    public async Task<IReadOnlyList<OperationalOption>> GetPublicComplaintOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT establishment.id AS Id,
                   company.razon_social || ' - ' || establishment.nombre AS Name,
                   establishment.empresa_id AS CompanyId
              FROM "SIGERSA"."ESTABLECIMIENTO" establishment
              JOIN "SIGERSA"."EMPRESA" company ON company.id = establishment.empresa_id
             WHERE establishment.activo = true AND establishment.estado = 'ACTIVO'
               AND company.activo = true AND company.estado = 'ACTIVA'
             ORDER BY company.razon_social, establishment.nombre;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
            return (await connection.QueryAsync<OperationalOption>(new CommandDefinition(
                Sql(sql), cancellationToken: cancellationToken))).ToArray();
    }

    public async Task<Guid> CreatePublicComplaintAsync(
        PublicComplaintDraft draft,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var coordinatorId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(Sql("""
                SELECT user_account.id
                  FROM "SIGERSA"."USUARIO" user_account
                  JOIN "SIGERSA"."USUARIO_ROL" user_role
                    ON user_role.usuario_id = user_account.id AND user_role.activo = true
                  JOIN "SIGERSA"."ROL" role ON role.id = user_role.rol_id
                  LEFT JOIN "SIGERSA"."DENUNCIA" complaint
                    ON complaint.coordinador_asignado_id = user_account.id AND complaint.resultado IS NULL
                 WHERE user_account.activo = true AND user_account.estado = 'ACTIVO'
                   AND role.codigo = 'COORDINADOR' AND role.activo = true
                 GROUP BY user_account.id, user_account.nombre_completo
                 ORDER BY COUNT(complaint.id), user_account.nombre_completo
                 LIMIT 1;
                """), transaction: transaction, cancellationToken: cancellationToken));
            if (!coordinatorId.HasValue)
                throw new InvalidOperationException("No hay coordinadores activos disponibles para recibir la denuncia.");

            var id = Guid.NewGuid();
            var inserted = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                WITH inserted_complaint AS (
                INSERT INTO "SIGERSA"."DENUNCIA"
                    (id, numero, establecimiento_id, tipo_denuncia, fecha_recepcion, canal,
                     es_anonima, es_confidencial, descripcion, registrada_por,
                     coordinador_asignado_id, creado_por)
                SELECT @Id,
                       'DEN-' || to_char(CURRENT_TIMESTAMP, 'YYYYMMDD') || '-' || upper(substr(replace(@Id::text, '-', ''), 1, 8)),
                       establishment.id, @ComplaintType, CURRENT_TIMESTAMP, 'PORTAL_PUBLICO',
                       true, @IsConfidential, @Description, @CoordinatorId, @CoordinatorId, @CoordinatorId
                  FROM "SIGERSA"."ESTABLECIMIENTO" establishment
                 WHERE establishment.id = @EstablishmentId AND establishment.activo = true
                RETURNING id)

                INSERT INTO "SIGERSA"."NOTIFICACION"
                    (id, usuario_id, tipo, titulo, mensaje, recurso_tipo, recurso_id,
                     canal, estado, programada_para, creado_por)
                SELECT gen_random_uuid(), @CoordinatorId, 'DENUNCIA_PUBLICA_ASIGNADA',
                        'Nueva denuncia ciudadana asignada',
                        'Se recibió una denuncia desde el portal público y fue asignada para coordinación.',
                        'DENUNCIA', inserted_complaint.id, 'INTERNA', 'PENDIENTE', CURRENT_TIMESTAMP, @CoordinatorId
                  FROM inserted_complaint;
                """), new
            {
                Id = id,
                draft.EstablishmentId,
                draft.ComplaintType,
                draft.Description,
                draft.IsConfidential,
                CoordinatorId = coordinatorId.Value
            }, transaction, cancellationToken: cancellationToken));
            if (inserted != 1) throw new ArgumentException("El establecimiento seleccionado no está disponible.");
            await transaction.CommitAsync(cancellationToken);
            return id;
        }
    }

    public async Task<FindingsPage> SearchFindingsAsync(
        FindingSearch search,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT finding.id AS Id, finding.evaluacion_id AS EvaluationId,
                   evaluation.numero AS EvaluationNumber, establishment.nombre AS EstablishmentName,
                   item.source_allitems_item AS SourceItem, item.titulo AS ItemTitle,
                   criticality.nombre AS Criticality, finding.codigo AS Code,
                   finding.descripcion AS Description, finding.estado AS Status,
                   finding.detectada_en AS DetectedAt, finding.cerrada_en AS ClosedAt,
                   finding.version_fila AS RowVersion
              FROM "SIGERSA"."NO_CONFORMIDAD" finding
              JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.id = finding.evaluacion_id
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
              JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = finding.item_ficha_id
              JOIN "SIGERSA"."NIVEL_CRITICIDAD" criticality ON criticality.id = finding.nivel_criticidad_id
             WHERE {EvaluationAccess}
               AND (@Status IS NULL OR finding.estado = @Status)
               AND (@Search IS NULL OR lower(finding.codigo) LIKE '%' || @Search || '%'
                    OR lower(evaluation.numero) LIKE '%' || @Search || '%'
                    OR lower(establishment.nombre) LIKE '%' || @Search || '%'
                    OR lower(finding.descripcion) LIKE '%' || @Search || '%')
             ORDER BY finding.detectada_en DESC, finding.id
             OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
              FROM "SIGERSA"."NO_CONFORMIDAD" finding
              JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.id = finding.evaluacion_id
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
             WHERE {EvaluationAccess}
               AND (@Status IS NULL OR finding.estado = @Status)
               AND (@Search IS NULL OR lower(finding.codigo) LIKE '%' || @Search || '%'
                    OR lower(evaluation.numero) LIKE '%' || @Search || '%'
                    OR lower(establishment.nombre) LIKE '%' || @Search || '%'
                    OR lower(finding.descripcion) LIKE '%' || @Search || '%');
            """;
        var parameters = new
        {
            search.Search,
            search.Status,
            Offset = (search.Page - 1) * search.PageSize,
            search.PageSize,
            search.Scope.UserId,
            search.Scope.CompanyId,
            search.Scope.GlobalScope,
            search.Scope.AssignedOnly,
            search.Scope.OwnerOnly
        };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), parameters, cancellationToken: cancellationToken));
            var items = (await result.ReadAsync<FindingRow>()).Select(row => row.ToDomain()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new FindingsPage(items, search.Page, search.PageSize, total);
        }
    }

    public async Task<FindingDetail?> GetFindingAsync(
        Guid id,
        OperationalActorScope scope,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT finding.id AS Id, finding.codigo AS Code,
                   evaluation.numero AS EvaluationNumber, inspection_case.numero AS CaseNumber,
                   company.razon_social AS CompanyName, establishment.nombre AS EstablishmentName,
                   item.source_allitems_item AS SourceItem, item.codigo AS ItemCode,
                   item.titulo AS ItemTitle, response.valor_texto AS Rating,
                   criticality.nombre AS Criticality, finding.descripcion AS Description,
                   response.observacion AS Observation, response.comentario AS TechnicalComment,
                   finding.estado AS Status, finding.detectada_en AS DetectedAt
              FROM "SIGERSA"."NO_CONFORMIDAD" finding
              JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.id = finding.evaluacion_id
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
              JOIN "SIGERSA"."EMPRESA" company ON company.id = inspection_case.empresa_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
              JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = finding.item_ficha_id
              LEFT JOIN "SIGERSA"."RESPUESTA_USUARIO" response ON response.id = finding.respuesta_usuario_id
              JOIN "SIGERSA"."NIVEL_CRITICIDAD" criticality ON criticality.id = finding.nivel_criticidad_id
             WHERE finding.id = @Id AND {EvaluationAccess};

            SELECT evidence.id AS Id, evidence.nombre_original AS OriginalName,
                   evidence.mime_type AS MimeType, evidence.tipo_evidencia AS EvidenceType,
                   evidence.fecha_servidor AS UploadedAt
              FROM "SIGERSA"."EVIDENCIA" evidence
              JOIN "SIGERSA"."EVIDENCIA_NO_CONFORMIDAD" link ON link.evidencia_id = evidence.id
             WHERE link.no_conformidad_id = @Id AND evidence.eliminada = false
             ORDER BY evidence.fecha_servidor, evidence.id;
            """;
        var parameters = new
        {
            Id = id,
            scope.UserId,
            scope.CompanyId,
            scope.GlobalScope,
            scope.AssignedOnly,
            scope.OwnerOnly
        };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), parameters, cancellationToken: cancellationToken));
            var header = await result.ReadSingleOrDefaultAsync<FindingDetailRow>();
            var evidences = (await result.ReadAsync<FindingEvidenceRow>()).Select(row => row.ToDomain()).ToArray();
            return header?.ToDomain(evidences);
        }
    }

    public async Task<FindingOptions> GetFindingOptionsAsync(
        Guid actorId,
        bool canCreate,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT evaluation.id AS Id,
                   evaluation.numero || ' · ' || establishment.nombre AS Name,
                   inspection_case.empresa_id AS CompanyId
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
             WHERE @CanCreate = true AND evaluation.estado IN ('ASIGNADA', 'EN_EJECUCION', 'EN_CORRECCION')
               AND (evaluation.evaluador_principal_id = @ActorId OR EXISTS (
                    SELECT 1 FROM "SIGERSA"."USUARIO_ROL" user_role
                    JOIN "SIGERSA"."ROL" role ON role.id = user_role.rol_id
                    WHERE user_role.usuario_id = @ActorId AND user_role.activo = true
                      AND role.codigo = 'ADMINISTRADOR'))
             ORDER BY evaluation.modificado_en DESC;

            SELECT id AS Id, nombre AS Name, NULL::uuid AS CompanyId
              FROM "SIGERSA"."NIVEL_CRITICIDAD"
             WHERE activo = true
             ORDER BY prioridad, nombre;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), new { ActorId = actorId, CanCreate = canCreate }, cancellationToken: cancellationToken));
            var evaluations = (await result.ReadAsync<OperationalOption>()).ToArray();
            var criticalities = (await result.ReadAsync<OperationalOption>()).ToArray();
            return new FindingOptions(evaluations, criticalities, canCreate);
        }
    }

    public async Task<Guid> CreateFindingAsync(
        FindingDraft draft,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."NO_CONFORMIDAD"
                    (id, evaluacion_id, item_ficha_id, nivel_criticidad_id,
                     codigo, descripcion, estado, creado_por)
                SELECT @Id, evaluation.id, item.id, criticality.id,
                       'NC-' || upper(substr(replace(@Id::text, '-', ''), 1, 10)),
                       @Description, 'ABIERTA', @ActorId
                  FROM "SIGERSA"."EVALUACION" evaluation
                  JOIN "SIGERSA"."ITEM_FICHA" item
                    ON item.ficha_inspeccion_id = evaluation.ficha_inspeccion_id
                   AND item.source_allitems_item = @SourceItem AND item.es_evaluable = true
                  JOIN "SIGERSA"."NIVEL_CRITICIDAD" criticality
                    ON criticality.id = @CriticalityId AND criticality.activo = true
                 WHERE evaluation.id = @EvaluationId
                   AND evaluation.estado IN ('ASIGNADA', 'EN_EJECUCION', 'EN_CORRECCION')
                   AND (evaluation.evaluador_principal_id = @ActorId OR EXISTS (
                        SELECT 1 FROM "SIGERSA"."USUARIO_ROL" user_role
                        JOIN "SIGERSA"."ROL" role ON role.id = user_role.rol_id
                        WHERE user_role.usuario_id = @ActorId AND user_role.activo = true
                          AND role.codigo = 'ADMINISTRADOR'));
                """), new { Id = id, draft.EvaluationId, draft.SourceItem, draft.CriticalityId, draft.Description, ActorId = actorId }, cancellationToken: cancellationToken));
            if (affected != 1) throw new ArgumentException("La evaluación, el ítem o la criticidad no está disponible.");
        }
        return id;
    }

    public async Task<bool> CloseFindingAsync(
        Guid id,
        long rowVersion,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        return await UpdateFindingStatusAsync(
            id, rowVersion, "CERRADO", reason, actorId, cancellationToken);
    }

    public async Task<bool> UpdateFindingStatusAsync(
        Guid id,
        long rowVersion,
        string status,
        string? reason,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "SIGERSA"."NO_CONFORMIDAD"
               SET estado = @Status,
                   cerrada_en = CASE WHEN @Status = 'CERRADO' THEN CURRENT_TIMESTAMP ELSE NULL END,
                   cierre_justificacion = CASE WHEN @Status = 'CERRADO' THEN @Reason ELSE NULL END,
                   modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId,
                   version_fila = version_fila + 1
             WHERE id = @Id AND version_fila = @RowVersion
               AND ((estado = 'ABIERTA' AND @Status = 'EN_CORRECCION')
                    OR (estado = 'EN_CORRECCION' AND @Status = 'VALIDADO')
                    OR (estado = 'VALIDADO' AND @Status = 'CERRADO'));
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.ExecuteAsync(new CommandDefinition(
                Sql(sql), new { Id = id, RowVersion = rowVersion, Status = status, Reason = reason, ActorId = actorId },
                cancellationToken: cancellationToken)) == 1;
        }
    }

    public async Task<HistoricalEvaluationsPage> SearchHistoryAsync(
        HistoricalEvaluationSearch search,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT evaluation.id AS Id, evaluation.numero AS Number,
                   inspection_case.numero AS CaseNumber, company.razon_social AS CompanyName,
                   establishment.nombre AS EstablishmentName, evaluator.nombre_completo AS EvaluatorName,
                   evaluation.estado AS Status, evaluation.porcentaje_cumplimiento AS CompliancePercentage,
                   evaluation.riesgo_total AS TotalRisk, evaluation.nivel_riesgo AS RiskLevel,
                   evaluation.frecuencia AS Frequency, evaluation.creado_en AS CreatedAt,
                   evaluation.cerrada_en AS ClosedAt, report.id AS ReportId,
                   report.numero AS ReportNumber, report.estado AS ReportStatus,
                   EXISTS (SELECT 1 FROM "SIGERSA"."INFORME_VERSION" version
                           WHERE version.informe_id = report.id AND version.es_oficial = true) AS HasOfficialReport
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
              JOIN "SIGERSA"."EMPRESA" company ON company.id = inspection_case.empresa_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
              JOIN "SIGERSA"."USUARIO" evaluator ON evaluator.id = evaluation.evaluador_principal_id
              LEFT JOIN "SIGERSA"."SOLICITUD" request ON request.id = inspection_case.solicitud_id
              LEFT JOIN "SIGERSA"."INFORME" report ON report.evaluacion_id = evaluation.id AND report.tipo = 'EVALUACION_BPM'
             WHERE {EvaluationAccess}
               AND (@Status IS NULL OR evaluation.estado = @Status)
               AND (@From IS NULL OR evaluation.creado_en >= @From)
               AND (@To IS NULL OR evaluation.creado_en < @To)
               AND (@Search IS NULL OR lower(evaluation.numero) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(request.numero, '')) LIKE '%' || @Search || '%'
                    OR lower(inspection_case.numero) LIKE '%' || @Search || '%'
                    OR lower(company.razon_social) LIKE '%' || @Search || '%'
                    OR lower(establishment.nombre) LIKE '%' || @Search || '%')
             ORDER BY evaluation.creado_en DESC, evaluation.id
             OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
              JOIN "SIGERSA"."EMPRESA" company ON company.id = inspection_case.empresa_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
              LEFT JOIN "SIGERSA"."SOLICITUD" request ON request.id = inspection_case.solicitud_id
             WHERE {EvaluationAccess}
               AND (@Status IS NULL OR evaluation.estado = @Status)
               AND (@From IS NULL OR evaluation.creado_en >= @From)
               AND (@To IS NULL OR evaluation.creado_en < @To)
               AND (@Search IS NULL OR lower(evaluation.numero) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(request.numero, '')) LIKE '%' || @Search || '%'
                    OR lower(inspection_case.numero) LIKE '%' || @Search || '%'
                    OR lower(company.razon_social) LIKE '%' || @Search || '%'
                    OR lower(establishment.nombre) LIKE '%' || @Search || '%');
            """;
        var parameters = new
        {
            search.Search,
            search.Status,
            search.From,
            search.To,
            Offset = (search.Page - 1) * search.PageSize,
            search.PageSize,
            search.Scope.UserId,
            search.Scope.CompanyId,
            search.Scope.GlobalScope,
            search.Scope.AssignedOnly,
            search.Scope.OwnerOnly
        };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), parameters, cancellationToken: cancellationToken));
            var items = (await result.ReadAsync<HistoricalRow>()).Select(row => row.ToDomain()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new HistoricalEvaluationsPage(items, search.Page, search.PageSize, total);
        }
    }

    public async Task<IReadOnlyList<TimelineEvent>> GetTimelineAsync(
        Guid evaluationId,
        OperationalActorScope scope,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            WITH allowed AS (
                SELECT evaluation.id, evaluation.caso_id
                  FROM "SIGERSA"."EVALUACION" evaluation
                  JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
                 WHERE evaluation.id = @EvaluationId AND {EvaluationAccess}
            ), events AS (
                SELECT evaluation.creado_en AS OccurredAt, 'EVALUACION'::varchar AS EventType,
                       'Evaluación creada'::varchar AS Title, evaluation.estado::text AS Detail,
                       creator.nombre_completo AS ActorName
                  FROM "SIGERSA"."EVALUACION" evaluation
                  JOIN allowed ON allowed.id = evaluation.id
                  LEFT JOIN "SIGERSA"."USUARIO" creator ON creator.id = evaluation.creado_por
                UNION ALL
                SELECT evaluation.cancelada_en, 'EVALUACION', 'Inspección cancelada',
                       evaluation.motivo_cancelacion, actor.nombre_completo
                  FROM "SIGERSA"."EVALUACION" evaluation
                  JOIN allowed ON allowed.id = evaluation.id
                  LEFT JOIN "SIGERSA"."USUARIO" actor ON actor.id = evaluation.modificado_por
                 WHERE evaluation.estado = 'CANCELADA' AND evaluation.cancelada_en IS NOT NULL
                UNION ALL
                SELECT transition.ejecutado_en, 'CASO',
                       'Cambio de estado: ' || transition.estado_nuevo,
                       transition.motivo, actor.nombre_completo
                  FROM "SIGERSA"."CASO_TRANSICION" transition
                  JOIN allowed ON allowed.caso_id = transition.caso_id
                  LEFT JOIN "SIGERSA"."USUARIO" actor ON actor.id = transition.ejecutado_por
                UNION ALL
                SELECT correction.solicitada_en, 'CORRECCION',
                       'Corrección ' || correction.estado, correction.observacion_coordinador,
                       actor.nombre_completo
                  FROM "SIGERSA"."CORRECCION" correction
                  JOIN allowed ON allowed.id = correction.evaluacion_id
                  LEFT JOIN "SIGERSA"."USUARIO" actor ON actor.id = correction.solicitada_por
                UNION ALL
                SELECT evidence.fecha_servidor, 'EVIDENCIA', 'Evidencia cargada',
                       evidence.nombre_original, actor.nombre_completo
                  FROM "SIGERSA"."EVIDENCIA" evidence
                  JOIN allowed ON allowed.id = evidence.evaluacion_id
                  LEFT JOIN "SIGERSA"."USUARIO" actor ON actor.id = evidence.subida_por
                UNION ALL
                SELECT version.generado_en, 'INFORME',
                       CASE WHEN version.es_oficial THEN 'Informe oficial emitido' ELSE 'Informe generado' END,
                       report.numero, actor.nombre_completo
                  FROM "SIGERSA"."INFORME_VERSION" version
                  JOIN "SIGERSA"."INFORME" report ON report.id = version.informe_id
                  JOIN allowed ON allowed.id = report.evaluacion_id
                  LEFT JOIN "SIGERSA"."USUARIO" actor ON actor.id = version.generado_por
            )
            SELECT * FROM events ORDER BY OccurredAt, EventType;
            """;
        var parameters = new
        {
            EvaluationId = evaluationId,
            scope.UserId,
            scope.CompanyId,
            scope.GlobalScope,
            scope.AssignedOnly,
            scope.OwnerOnly
        };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var rows = (await connection.QueryAsync<TimelineRow>(new CommandDefinition(
                Sql(sql), parameters, cancellationToken: cancellationToken))).Select(row => row.ToDomain()).ToArray();
            if (rows.Length == 0) throw new KeyNotFoundException("La evaluación no existe o no está disponible para el usuario.");
            return rows;
        }
    }

    public async Task<AuditEventsPage> SearchAuditAsync(
        AuditEventSearch search,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT audit.id AS Id, audit.ocurrido_en AS OccurredAt,
                   audit.accion AS Action, audit.recurso_tipo AS ResourceType,
                   audit.recurso_id AS ResourceId, audit.resultado AS Result,
                   actor.nombre_completo AS ActorName, audit.motivo AS Reason,
                   audit.correlacion_id AS CorrelationId
              FROM "SIGERSA"."AUDITORIA_EVENTO" audit
              LEFT JOIN "SIGERSA"."USUARIO" actor ON actor.id = audit.actor_id
             WHERE (@Result IS NULL OR audit.resultado = @Result)
               AND (@From IS NULL OR audit.ocurrido_en >= @From)
               AND (@To IS NULL OR audit.ocurrido_en < @To)
               AND (@Search IS NULL OR lower(audit.accion) LIKE '%' || @Search || '%'
                    OR lower(audit.recurso_tipo) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(audit.recurso_id::text, '')) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(actor.nombre_completo, '')) LIKE '%' || @Search || '%')
             ORDER BY audit.ocurrido_en DESC, audit.id
             OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*) FROM "SIGERSA"."AUDITORIA_EVENTO" audit
              LEFT JOIN "SIGERSA"."USUARIO" actor ON actor.id = audit.actor_id
             WHERE (@Result IS NULL OR audit.resultado = @Result)
               AND (@From IS NULL OR audit.ocurrido_en >= @From)
               AND (@To IS NULL OR audit.ocurrido_en < @To)
               AND (@Search IS NULL OR lower(audit.accion) LIKE '%' || @Search || '%'
                    OR lower(audit.recurso_tipo) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(audit.recurso_id::text, '')) LIKE '%' || @Search || '%'
                    OR lower(COALESCE(actor.nombre_completo, '')) LIKE '%' || @Search || '%');
            """;
        var parameters = new
        {
            search.Search,
            search.Result,
            search.From,
            search.To,
            Offset = (search.Page - 1) * search.PageSize,
            search.PageSize
        };
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), parameters, cancellationToken: cancellationToken));
            var items = (await result.ReadAsync<AuditRow>()).Select(row => row.ToDomain()).ToArray();
            var total = await result.ReadSingleAsync<int>();
            return new AuditEventsPage(items, search.Page, search.PageSize, total);
        }
    }

    public async Task<ReportGenerationData?> GetReportDataAsync(
        Guid evaluationId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT evaluation.id AS EvaluationId, evaluation.numero AS EvaluationNumber,
                   inspection_case.numero AS CaseNumber, company.razon_social AS CompanyName,
                   establishment.nombre AS EstablishmentName,
                   concat_ws(' ', establishment.calle, establishment.numero_direccion) AS Address,
                   evaluator.nombre_completo AS EvaluatorName, evaluation.estado AS Status,
                   evaluation.porcentaje_cumplimiento AS CompliancePercentage,
                   evaluation.riesgo_producto AS ProductRisk,
                   evaluation.riesgo_establecimiento AS EstablishmentRisk,
                   evaluation.riesgo_total AS TotalRisk, evaluation.nivel_riesgo AS RiskLevel,
                   evaluation.frecuencia AS Frequency, evaluation.iniciada_en AS StartedAt,
                   evaluation.finalizada_en AS FinishedAt,
                   COALESCE(complement.medidas_correctivas, '[]'::jsonb)::text AS CorrectiveMeasuresJson,
                   COALESCE(complement.recomendaciones, '[]'::jsonb)::text AS RecommendationsJson
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
              JOIN "SIGERSA"."EMPRESA" company ON company.id = inspection_case.empresa_id
              JOIN "SIGERSA"."ESTABLECIMIENTO" establishment ON establishment.id = evaluation.establecimiento_id
              JOIN "SIGERSA"."USUARIO" evaluator ON evaluator.id = evaluation.evaluador_principal_id
              LEFT JOIN "SIGERSA"."EVALUACION_COMPLEMENTO" complement ON complement.evaluacion_id = evaluation.id
             WHERE evaluation.id = @EvaluationId
               AND evaluation.estado IN ('FINALIZADA', 'ENVIADA', 'EN_REVISION', 'EN_CORRECCION', 'APROBADA', 'NO_APROBADA', 'CERRADA')
               AND EXISTS (
                    SELECT 1 FROM "SIGERSA"."USUARIO_ROL" user_role
                    JOIN "SIGERSA"."ROL" role ON role.id = user_role.rol_id
                    WHERE user_role.usuario_id = @ActorId AND user_role.activo = true
                      AND role.codigo IN ('ADMINISTRADOR', 'COORDINADOR'));

            SELECT finding.codigo AS Code, criticality.nombre AS Criticality,
                   finding.descripcion AS Description, finding.estado AS Status
              FROM "SIGERSA"."NO_CONFORMIDAD" finding
              JOIN "SIGERSA"."NIVEL_CRITICIDAD" criticality ON criticality.id = finding.nivel_criticidad_id
             WHERE finding.evaluacion_id = @EvaluationId
             ORDER BY finding.detectada_en, finding.codigo;

            SELECT nombre_original AS Name, tipo_evidencia AS Type, mime_type AS MimeType,
                   bucket_name AS BucketName, supabase_path AS SupabasePath
              FROM "SIGERSA"."EVIDENCIA"
             WHERE evaluacion_id = @EvaluationId AND eliminada = false
             ORDER BY fecha_servidor, id;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            using var result = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql(sql), new { EvaluationId = evaluationId, ActorId = actorId }, cancellationToken: cancellationToken));
            var header = await result.ReadSingleOrDefaultAsync<ReportHeaderRow>();
            var findings = (await result.ReadAsync<ReportFinding>()).ToArray();
            var evidences = (await result.ReadAsync<ReportEvidence>()).ToArray();
            return header?.ToDomain(findings, evidences);
        }
    }

    public async Task<ReportFileReference> SaveReportVersionAsync(
        Guid evaluationId,
        string bucketName,
        string supabasePath,
        long fileSize,
        string hash,
        bool isOfficial,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var status = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(Sql("""
                SELECT evaluation.estado
                  FROM "SIGERSA"."EVALUACION" evaluation
                 WHERE evaluation.id = @EvaluationId
                 FOR UPDATE;
                """), new { EvaluationId = evaluationId }, transaction, cancellationToken: cancellationToken));
            if (status is null || status is "ASIGNADA" or "EN_EJECUCION")
                throw new ArgumentException("La evaluación todavía no está lista para generar un informe.");
            if (isOfficial && status is not ("APROBADA" or "NO_APROBADA"))
                throw new InvalidOperationException(
                    "El informe oficial solo puede emitirse después de aprobar o no aprobar la evaluación.");

            var reportId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."INFORME"
                    (id, evaluacion_id, numero, tipo, estado, version_actual, creado_por)
                VALUES (@Id, @EvaluationId,
                        'INF-' || to_char(CURRENT_TIMESTAMP, 'YYYYMMDD') || '-' || upper(substr(replace(@Id::text, '-', ''), 1, 8)),
                        'EVALUACION_BPM', 'BORRADOR', 0, @ActorId)
                ON CONFLICT (evaluacion_id, tipo) DO UPDATE
                    SET modificado_en = CURRENT_TIMESTAMP, modificado_por = @ActorId
                RETURNING id;
                """), new { Id = Guid.NewGuid(), EvaluationId = evaluationId, ActorId = actorId }, transaction, cancellationToken: cancellationToken));
            var alreadyOfficial = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(Sql("""
                SELECT EXISTS (
                    SELECT 1
                      FROM "SIGERSA"."INFORME_VERSION"
                     WHERE informe_id = @ReportId AND es_oficial = true
                );
                """), new { ReportId = reportId }, transaction, cancellationToken: cancellationToken));
            if (alreadyOfficial)
                throw new InvalidOperationException(
                    "El informe oficial ya fue emitido y sus versiones quedaron cerradas para edición.");
            var version = await connection.ExecuteScalarAsync<int>(new CommandDefinition(Sql("""
                SELECT COALESCE(MAX(version), 0) + 1
                  FROM "SIGERSA"."INFORME_VERSION" WHERE informe_id = @ReportId;
                """), new { ReportId = reportId }, transaction, cancellationToken: cancellationToken));
            var versionId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(Sql("""
                INSERT INTO "SIGERSA"."INFORME_VERSION"
                    (id, informe_id, version, bucket_name, supabase_path, file_size,
                     mime_type, hash, es_oficial, generado_por, emitido_por, emitido_en, creado_por)
                VALUES (@VersionId, @ReportId, @Version, @BucketName, @SupabasePath, @FileSize,
                        'application/pdf', @Hash, @IsOfficial, @ActorId,
                        CASE WHEN @IsOfficial THEN @ActorId END,
                        CASE WHEN @IsOfficial THEN CURRENT_TIMESTAMP END, @ActorId);

                UPDATE "SIGERSA"."INFORME"
                   SET estado = CASE WHEN @IsOfficial THEN 'EMITIDO' ELSE 'BORRADOR' END,
                       version_actual = @Version, modificado_en = CURRENT_TIMESTAMP,
                       modificado_por = @ActorId, version_fila = version_fila + 1
                 WHERE id = @ReportId;
                """), new
            {
                VersionId = versionId,
                ReportId = reportId,
                Version = version,
                BucketName = bucketName,
                SupabasePath = supabasePath,
                FileSize = fileSize,
                Hash = hash,
                IsOfficial = isOfficial,
                ActorId = actorId
            }, transaction,
                cancellationToken: cancellationToken));
            var number = await connection.ExecuteScalarAsync<string>(new CommandDefinition(Sql("""
                SELECT numero FROM "SIGERSA"."INFORME" WHERE id = @ReportId;
                """), new { ReportId = reportId }, transaction, cancellationToken: cancellationToken))
                ?? throw new InvalidOperationException("No fue posible recuperar el número del informe generado.");
            await transaction.CommitAsync(cancellationToken);
            return new ReportFileReference(reportId, number, version, bucketName, supabasePath,
                "application/pdf", $"{number}-v{version}.pdf", isOfficial);
        }
    }

    public async Task<ReportFileReference?> GetReportFileAsync(
        Guid reportId,
        OperationalActorScope scope,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT report.id AS ReportId, report.numero AS ReportNumber, version.version AS Version,
                   version.bucket_name AS BucketName, version.supabase_path AS SupabasePath,
                   version.mime_type AS MimeType,
                   report.numero || '-v' || version.version || '.pdf' AS FileName,
                   version.es_oficial AS IsOfficial
              FROM "SIGERSA"."INFORME" report
              JOIN "SIGERSA"."INFORME_VERSION" version ON version.informe_id = report.id
              JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.id = report.evaluacion_id
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
             WHERE report.id = @ReportId AND {EvaluationAccess}
               AND (@GlobalScope = true OR @AssignedOnly = true OR version.es_oficial = true)
             ORDER BY version.es_oficial DESC, version.version DESC
             LIMIT 1;
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            return await connection.QuerySingleOrDefaultAsync<ReportFileReference>(new CommandDefinition(
                Sql(sql), new { ReportId = reportId, scope.UserId, scope.CompanyId, scope.GlobalScope, scope.AssignedOnly, scope.OwnerOnly },
                cancellationToken: cancellationToken));
        }
    }

    private static object ScopeParameters(OperationalActorScope scope) => new
    {
        scope.UserId,
        scope.CompanyId,
        scope.GlobalScope,
        scope.AssignedOnly,
        scope.OwnerOnly
    };

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed class DashboardEvaluationRow
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public string EstablishmentName { get; init; } = string.Empty; public string Status { get; init; } = string.Empty;
        public decimal? CompliancePercentage { get; init; }
        public string? RiskLevel { get; init; }
        public DateTime UpdatedAt { get; init; }
        public DashboardEvaluation ToDomain() => new(Id, Number, EstablishmentName, Status, CompliancePercentage, RiskLevel, Utc(UpdatedAt));
    }

    private sealed class DashboardScheduleRow
    {
        public Guid Id { get; init; }
        public string CaseNumber { get; init; } = string.Empty;
        public string EstablishmentName { get; init; } = string.Empty; public DateTime StartsAt { get; init; }
        public string Status { get; init; } = string.Empty;
        public DashboardUpcomingSchedule ToDomain() => new(Id, CaseNumber, EstablishmentName, Utc(StartsAt), Status);
    }

    private sealed class NotificationRow
    {
        public Guid Id { get; init; }
        public string Type { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty; public string Message { get; init; } = string.Empty;
        public string? ResourceType { get; init; }
        public Guid? ResourceId { get; init; }
        public DateTime ScheduledFor { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ReadAt { get; init; }
        public NotificationRecord ToDomain() => new(Id, Type, Title, Message, ResourceType, ResourceId,
            Utc(ScheduledFor), Utc(CreatedAt), ReadAt.HasValue ? Utc(ReadAt.Value) : null);
    }

    private sealed class SurveillanceRow
    {
        public Guid Id { get; init; }
        public string Kind { get; init; } = string.Empty;
        public string Number { get; init; } = string.Empty; public DateTime OccurredAt { get; init; }
        public Guid? CompanyId { get; init; }
        public string? CompanyName { get; init; }
        public Guid? EstablishmentId { get; init; }
        public string? EstablishmentName { get; init; }
        public string Subject { get; init; } = string.Empty; public string Description { get; init; } = string.Empty;
        public short Priority { get; init; }
        public string? Channel { get; init; }
        public bool IsAnonymous { get; init; }
        public bool IsConfidential { get; init; }
        public string? Result { get; init; }
        public bool HasCase { get; init; }
        public long RowVersion { get; init; }
        public SurveillanceRecord ToDomain() => new(Id, Kind, Number, Utc(OccurredAt), CompanyId, CompanyName,
            EstablishmentId, EstablishmentName, Subject, Description, Priority, Channel,
            IsAnonymous, IsConfidential, Result, HasCase, RowVersion);
    }

    private sealed class FindingRow
    {
        public Guid Id { get; init; }
        public Guid EvaluationId { get; init; }
        public string EvaluationNumber { get; init; } = string.Empty; public string EstablishmentName { get; init; } = string.Empty;
        public int SourceItem { get; init; }
        public string ItemTitle { get; init; } = string.Empty;
        public string Criticality { get; init; } = string.Empty; public string Code { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty; public string Status { get; init; } = string.Empty;
        public DateTime DetectedAt { get; init; }
        public DateTime? ClosedAt { get; init; }
        public long RowVersion { get; init; }
        public FindingRecord ToDomain() => new(Id, EvaluationId, EvaluationNumber, EstablishmentName, SourceItem,
            ItemTitle, Criticality, Code, Description, Status, Utc(DetectedAt), ClosedAt.HasValue ? Utc(ClosedAt.Value) : null, RowVersion);
    }

    public async Task<bool> HasOfficialReportAsync(
        Guid evaluationId,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
            return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(Sql("""
                SELECT EXISTS (
                    SELECT 1
                      FROM "SIGERSA"."INFORME" report
                      JOIN "SIGERSA"."INFORME_VERSION" version ON version.informe_id = report.id
                     WHERE report.evaluacion_id = @EvaluationId AND version.es_oficial = true
                );
                """), new { EvaluationId = evaluationId }, cancellationToken: cancellationToken));
    }

    private sealed class FindingDetailRow
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string EvaluationNumber { get; init; } = string.Empty;
        public string CaseNumber { get; init; } = string.Empty;
        public string CompanyName { get; init; } = string.Empty;
        public string EstablishmentName { get; init; } = string.Empty;
        public int SourceItem { get; init; }
        public string ItemCode { get; init; } = string.Empty;
        public string ItemTitle { get; init; } = string.Empty;
        public string Rating { get; init; } = string.Empty;
        public string Criticality { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string? Observation { get; init; }
        public string? TechnicalComment { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime DetectedAt { get; init; }
        public FindingDetail ToDomain(IReadOnlyList<FindingEvidence> evidences) => new(
            Id, Code, EvaluationNumber, CaseNumber, CompanyName, EstablishmentName,
            SourceItem, ItemCode, ItemTitle, Rating, Criticality, Description,
            Observation, TechnicalComment, Status, Utc(DetectedAt), evidences);
    }

    private sealed class FindingEvidenceRow
    {
        public Guid Id { get; init; }
        public string OriginalName { get; init; } = string.Empty;
        public string MimeType { get; init; } = string.Empty;
        public string EvidenceType { get; init; } = string.Empty;
        public DateTime UploadedAt { get; init; }
        public FindingEvidence ToDomain() => new(Id, OriginalName, MimeType, EvidenceType, Utc(UploadedAt));
    }

    private sealed class HistoricalRow
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public string CaseNumber { get; init; } = string.Empty; public string CompanyName { get; init; } = string.Empty;
        public string EstablishmentName { get; init; } = string.Empty; public string EvaluatorName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty; public decimal? CompliancePercentage { get; init; }
        public decimal? TotalRisk { get; init; }
        public string? RiskLevel { get; init; }
        public string? Frequency { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ClosedAt { get; init; }
        public Guid? ReportId { get; init; }
        public string? ReportNumber { get; init; }
        public string? ReportStatus { get; init; }
        public bool HasOfficialReport { get; init; }
        public HistoricalEvaluation ToDomain() => new(Id, Number, CaseNumber, CompanyName, EstablishmentName,
            EvaluatorName, Status, CompliancePercentage, TotalRisk, RiskLevel, Frequency,
            Utc(CreatedAt), ClosedAt.HasValue ? Utc(ClosedAt.Value) : null,
            ReportId, ReportNumber, ReportStatus, HasOfficialReport);
    }

    private sealed class TimelineRow
    {
        public DateTime OccurredAt { get; init; }
        public string EventType { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty; public string? Detail { get; init; }
        public string? ActorName { get; init; }
        public TimelineEvent ToDomain() => new(Utc(OccurredAt), EventType, Title, Detail, ActorName);
    }

    private sealed class AuditRow
    {
        public Guid Id { get; init; }
        public DateTime OccurredAt { get; init; }
        public string Action { get; init; } = string.Empty; public string ResourceType { get; init; } = string.Empty;
        public Guid? ResourceId { get; init; }
        public string Result { get; init; } = string.Empty;
        public string? ActorName { get; init; }
        public string? Reason { get; init; }
        public Guid? CorrelationId { get; init; }
        public AuditEventRecord ToDomain() => new(Id, Utc(OccurredAt), Action, ResourceType, ResourceId, Result, ActorName, Reason, CorrelationId);
    }

    private sealed class ReportHeaderRow
    {
        private static readonly JsonSerializerOptions FollowUpJsonOptions =
            new() { PropertyNameCaseInsensitive = true };

        public Guid EvaluationId { get; init; }
        public string EvaluationNumber { get; init; } = string.Empty;
        public string CaseNumber { get; init; } = string.Empty; public string CompanyName { get; init; } = string.Empty;
        public string EstablishmentName { get; init; } = string.Empty; public string Address { get; init; } = string.Empty;
        public string EvaluatorName { get; init; } = string.Empty; public string Status { get; init; } = string.Empty;
        public decimal? CompliancePercentage { get; init; }
        public decimal? ProductRisk { get; init; }
        public decimal? EstablishmentRisk { get; init; }
        public decimal? TotalRisk { get; init; }
        public string? RiskLevel { get; init; }
        public string? Frequency { get; init; }
        public DateTime? StartedAt { get; init; }
        public DateTime? FinishedAt { get; init; }
        public string CorrectiveMeasuresJson { get; init; } = "[]";
        public string RecommendationsJson { get; init; } = "[]";
        public ReportGenerationData ToDomain(IReadOnlyList<ReportFinding> findings, IReadOnlyList<ReportEvidence> evidences) =>
            new(EvaluationId, EvaluationNumber, CaseNumber, CompanyName, EstablishmentName, Address,
                EvaluatorName, Status, CompliancePercentage, ProductRisk, EstablishmentRisk, TotalRisk,
                RiskLevel, Frequency, StartedAt.HasValue ? Utc(StartedAt.Value) : null,
                FinishedAt.HasValue ? Utc(FinishedAt.Value) : null, findings, evidences,
                DeserializeFollowUps(CorrectiveMeasuresJson), DeserializeFollowUps(RecommendationsJson));

        private static EvaluationFollowUpItem[] DeserializeFollowUps(string json) =>
            JsonSerializer.Deserialize<EvaluationFollowUpItem[]>(json, FollowUpJsonOptions) ?? [];
    }
}
