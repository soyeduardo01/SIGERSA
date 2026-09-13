using Dapper;
using Npgsql;
using SIGERSA.Application.Evaluations;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Infrastructure.Persistence;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var localSettings = File.ReadLines(Path.Combine(root, ".env.local"))
    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#') && line.Contains('='))
    .Select(line => line.Split('=', 2))
    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim().Trim('"'), StringComparer.Ordinal);
var connectionString = localSettings["Database__ConnectionString"];
await using var dataSource = NpgsqlDataSource.Create(connectionString);

if (args.Contains("--check-registration", StringComparer.Ordinal))
{
    await using var command = dataSource.CreateCommand("""
        SELECT
            to_regclass('"SIGERSA"."USUARIO"') IS NOT NULL AS usuario_table,
            to_regclass('"SIGERSA"."USUARIO_DOCUMENTO_AUTORIZACION"') IS NOT NULL AS document_table,
            EXISTS (
                SELECT 1
                  FROM information_schema.columns
                 WHERE table_schema = 'SIGERSA'
                   AND table_name = 'USUARIO'
                   AND column_name = 'rol_solicitado') AS requested_role_column,
            EXISTS (
                SELECT 1
                  FROM information_schema.columns
                 WHERE table_schema = 'SIGERSA'
                   AND table_name = 'USUARIO'
                   AND column_name = 'terminos_aceptados_en') AS terms_column;
        """);
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    Console.WriteLine(
        "Registration schema: usuario={0}, authorization_document={1}, requested_role={2}, terms={3}",
        reader.GetBoolean(0), reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3));

    var supabase = new Supabase.Client(
        localSettings["Supabase__Url"],
        localSettings["Supabase__Key"],
        new Supabase.SupabaseOptions { AutoConnectRealtime = false, AutoRefreshToken = false });
    var bucketName = localSettings["Supabase__DefaultBucketName"];
    try
    {
        var bucket = await supabase.Storage.GetBucket(bucketName);
        Console.WriteLine(
            "Registration storage: bucket={0}, private={1}",
            bucket?.Id == bucketName,
            bucket is not null && !bucket.Public);

        if (args.Contains("--check-registration-write", StringComparer.Ordinal))
        {
            var path = $"diagnostics/registration-{Guid.NewGuid():N}.png";
            var storage = supabase.Storage.From(bucketName);
            var uploaded = false;
            try
            {
                byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
                await storage.Upload(pngHeader, path, new Supabase.Storage.FileOptions
                {
                    ContentType = "image/png",
                    Upsert = false
                });
                uploaded = true;
                Console.WriteLine("Registration storage write: ok");
            }
            finally
            {
                if (uploaded)
                {
                    await storage.Remove(path);
                    Console.WriteLine("Registration storage cleanup: ok");
                }
            }
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine("Registration storage: unavailable ({0})", exception.Message);
        Environment.ExitCode = 2;
    }
    return;
}

if (args.Contains("--migrate-latest", StringComparer.Ordinal))
{
    foreach (var fileName in new[]
    {
        "013_complete_functional_support.sql",
        "014_two_factor_authentication.sql",
        "015_request_pending_assignment_status.sql",
        "016_public_user_registration.sql",
        "017_establishment_catalogs_and_generated_code.sql",
        "018_seed_establishment_types.sql",
        "019_evaluation_workspace.sql",
        "020_inspection_qualification_policy.sql"
    })
    {
        var migrationPath = Path.Combine(root, "src", "backend", "Database", "Migrations", fileName);
        await using var migrationCommand = dataSource.CreateCommand(await File.ReadAllTextAsync(migrationPath));
        migrationCommand.CommandTimeout = 120;
        await migrationCommand.ExecuteNonQueryAsync();
        Console.WriteLine($"Migration {Path.GetFileNameWithoutExtension(fileName)} applied successfully.");
    }
}

if (args.Contains("--check-focused-evaluation-write", StringComparer.Ordinal))
{
    await CheckFocusedEvaluationWriteAsync(dataSource);
    return;
}

var factory = new DbConnectionFactory(dataSource);
var actorId = Guid.Empty;

var requests = await new InspectionRequestRepository(factory).SearchAsync(
    new InspectionRequestSearch(null, null, 1, 5, actorId, null, true, false));
var cases = await new CaseRepository(factory).SearchAsync(
    new CaseSearch(null, null, 1, 5, actorId, null, true, false));
var schedules = await new SchedulingRepository(factory).SearchAsync(
    new ScheduleSearch(null, null, 1, 5, actorId, false));
var evaluationRepository = new EvaluationWorkflowRepository(factory);
var evaluations = await evaluationRepository.SearchAsync(
    new EvaluationSearch(null, null, 1, 5, actorId, null, true, false));
var evaluationOptions = await evaluationRepository.GetOptionsAsync(true);
EvaluationInspectionContext? inspectionContext = null;
if (evaluations.Items.Count > 0)
{
    var evaluation = evaluations.Items[0];
    _ = await evaluationRepository.GetWorkspaceAsync(evaluation.Id, evaluation.EvaluatorId);
    inspectionContext = await evaluationRepository.GetInspectionContextAsync(evaluation.Id, evaluation.EvaluatorId);
}
var establishmentOptions = await new EstablishmentRepository(factory).GetOptionsAsync();
var requestOptions = await new InspectionRequestRepository(factory).GetOptionsAsync(null, true);
AssertCatalog(
    requestOptions.EstablishmentTypes,
    ["Planta procesadora de alimentos", "Restaurante", "Supermercado", "Almacén o depósito", "Panadería", "Comedor", "Mercado", "Distribuidora", "Importadora", "Farmacia", "Otro"],
    "tipos de establecimiento");
AssertCatalog(
    establishmentOptions.Markets.Select(value => value.Name),
    ["Infantil", "Niños menores", "Adultos", "Mujeres embarazadas", "Adultos mayores", "Todos los segmentos"],
    "mercados objetivo");
AssertCatalog(
    establishmentOptions.Commercializations.Select(value => value.Name),
    ["Local", "Nacional", "Internacional", "Todos los mercados"],
    "comercialización");
AssertCatalog(
    establishmentOptions.HaccpLevels.Select(value => value.StringData ?? string.Empty),
    ["En el 25% de las líneas de producción", "En el 75% de las líneas de producción", "En todas las líneas de producción"],
    "niveles HACCP");
AssertCatalog(
    establishmentOptions.SamplingApplications.Select(value => value.StringData ?? string.Empty),
    ["Solo para las materias primas", "Solo para las áreas de proceso y productos terminados", "Para las materias primas, las áreas de proceso y productos terminados"],
    "muestreo microbiológico");
AssertCatalog(
    establishmentOptions.InabieDistributions.Select(value => value.StringData ?? string.Empty),
    ["A nivel nacional", "A nivel regional", "A nivel local"],
    "distribución INABIE");
var evidences = await new EvidenceRepository(factory).SearchAsync(
    new EvidenceSearch(null, null, 1, 5, actorId, null, true, false));
var corrections = await new CorrectionRepository(factory).SearchAsync(
    new CorrectionSearch(null, null, 1, 5, actorId, null, true, false));
var companies = await new CompanyRepository(factory).SearchAsync(new CompanySearch(null, null, 1, 5));
var canActivateUser = await new UsuarioRepository(factory).CanActivateAsync(Guid.Empty);
var supportingDocuments = new SupportingDocumentRepository(factory);
var canAttachUserDocument = await supportingDocuments.CanAttachToUserAsync(Guid.Empty, null, true);
var canAttachRequestDocument = await supportingDocuments.CanAttachToRequestAsync(Guid.Empty, Guid.Empty, null, true);
var operations = new OperationalRepository(factory);
var operationalScope = new OperationalActorScope(actorId, null, true, false);
var dashboard = await operations.GetDashboardAsync(operationalScope);
var surveillance = await operations.SearchSurveillanceAsync(
    new SurveillanceSearch(null, null, null, 1, 5, operationalScope));
var findings = await operations.SearchFindingsAsync(
    new FindingSearch(null, null, 1, 5, operationalScope));
var findingOptions = await operations.GetFindingOptionsAsync(actorId, true);
var history = await operations.SearchHistoryAsync(
    new HistoricalEvaluationSearch(null, null, null, null, 1, 5, operationalScope));
var audit = await operations.SearchAuditAsync(new AuditEventSearch(null, null, null, null, 1, 5));

Console.WriteLine($"SQL smoke OK: requests={requests.Total}, cases={cases.Total}, schedules={schedules.Total}, evaluations={evaluations.Total}, evaluationOrigin={inspectionContext?.Origin ?? "none"}, evaluationOptions={evaluationOptions.Cases.Count + evaluationOptions.Establishments.Count + evaluationOptions.Templates.Count + evaluationOptions.RiskRules.Count + evaluationOptions.Evaluators.Count}, establishmentTypes={requestOptions.EstablishmentTypes.Count}, establishmentCatalogs={establishmentOptions.Markets.Count + establishmentOptions.Commercializations.Count + establishmentOptions.HaccpLevels.Count + establishmentOptions.SamplingApplications.Count + establishmentOptions.InabieDistributions.Count}, evidences={evidences.Total}, corrections={corrections.Total}, companies={companies.Total}, dashboard={dashboard.ActiveCases}, surveillance={surveillance.Total}, findings={findings.Total}, findingOptions={findingOptions.Evaluations.Count + findingOptions.Criticalities.Count}, history={history.Total}, audit={audit.Total}, activation={canActivateUser}, userDocument={canAttachUserDocument}, requestDocument={canAttachRequestDocument}");

static void AssertCatalog(IEnumerable<string> actual, string[] expected, string name)
{
    var actualValues = actual.ToHashSet(StringComparer.Ordinal);
    if (expected.Any(value => !actualValues.Contains(value)))
        throw new InvalidOperationException($"El catálogo de {name} no contiene todos los valores esperados.");
}

static async Task CheckFocusedEvaluationWriteAsync(NpgsqlDataSource dataSource)
{
    var previousCaseId = Guid.NewGuid();
    var currentCaseId = Guid.NewGuid();
    var previousEvaluationId = Guid.NewGuid();
    var currentEvaluationId = Guid.NewGuid();
    var previousResponseId = Guid.NewGuid();
    var answerIdempotencyKey = Guid.NewGuid();
    var correctionIdempotencyKey = Guid.NewGuid();
    var rejectedCorrectionIdempotencyKey = Guid.NewGuid();
    var evidenceIdempotencyKey = Guid.NewGuid();
    var marker = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    await using var connection = await dataSource.OpenConnectionAsync();
    try
    {
        var seed = await connection.QuerySingleAsync<FocusedEvaluationSeed>("""
            SELECT evaluation.establecimiento_id AS EstablishmentId,
                   inspection_case.empresa_id AS CompanyId,
                   evaluation.ficha_inspeccion_id AS InspectionTemplateId,
                   evaluation.evaluador_principal_id AS EvaluatorId,
                   (SELECT user_role.usuario_id
                      FROM "SIGERSA"."USUARIO_ROL" user_role
                      JOIN "SIGERSA"."ROL" role ON role.id = user_role.rol_id
                     WHERE user_role.activo = true AND role.codigo = 'COORDINADOR'
                     LIMIT 1) AS CoordinatorId,
                   (SELECT user_role.usuario_id
                      FROM "SIGERSA"."USUARIO_ROL" user_role
                      JOIN "SIGERSA"."ROL" role ON role.id = user_role.rol_id
                     WHERE user_role.activo = true AND role.codigo = 'ADMINISTRADOR'
                     LIMIT 1) AS AdministratorId,
                   evaluation.version_regla_riesgo_id AS RiskRuleVersionId,
                   item.id AS ItemId,
                   item.source_allitems_item AS SourceItem,
                   (SELECT COUNT(*)::integer
                      FROM "SIGERSA"."ITEM_FICHA" evaluable
                     WHERE evaluable.ficha_inspeccion_id = evaluation.ficha_inspeccion_id
                       AND evaluable.es_evaluable = true
                       AND evaluable.activo = true) AS TotalEvaluableItems,
                   (SELECT id FROM "SIGERSA"."MOTIVO_INSPECCION"
                     WHERE codigo = 'VIGILANCIA_CONTROL_RUTINA' AND activo = true LIMIT 1) AS ReasonId,
                   (SELECT id FROM "SIGERSA"."NIVEL_CRITICIDAD"
                     WHERE codigo = 'M' AND activo = true LIMIT 1) AS MajorCriticalityId
              FROM "SIGERSA"."EVALUACION" evaluation
              JOIN "SIGERSA"."CASO" inspection_case ON inspection_case.id = evaluation.caso_id
              JOIN "SIGERSA"."ITEM_FICHA" item ON item.ficha_inspeccion_id = evaluation.ficha_inspeccion_id
             WHERE item.es_evaluable = true AND item.activo = true
               AND item.source_allitems_item IS NOT NULL
             ORDER BY evaluation.creado_en DESC, item.orden
             LIMIT 1;
            """);
        if (seed.TotalEvaluableItems <= 1)
            throw new InvalidOperationException("La ficha disponible no tiene suficientes ítems para probar un alcance focalizado.");
        if (seed.ReasonId == Guid.Empty || seed.MajorCriticalityId == Guid.Empty
            || seed.CoordinatorId == Guid.Empty || seed.AdministratorId == Guid.Empty)
            throw new InvalidOperationException("Faltan los catálogos o usuarios necesarios para validar el flujo.");

        await connection.ExecuteAsync("""
            INSERT INTO "SIGERSA"."CASO"
                (id, numero, empresa_id, establecimiento_id, motivo_inspeccion_id,
                 origen, estado, prioridad, responsable_actual_id, creado_por)
            VALUES
                (@PreviousCaseId, @PreviousCaseNumber, @CompanyId, @EstablishmentId, @ReasonId,
                 'PROGRAMACION', 'ABIERTO', 3, @EvaluatorId, @EvaluatorId),
                (@CurrentCaseId, @CurrentCaseNumber, @CompanyId, @EstablishmentId, @ReasonId,
                 'PROGRAMACION', 'ABIERTO', 3, @EvaluatorId, @EvaluatorId);

            INSERT INTO "SIGERSA"."EVALUACION"
                (id, numero, caso_id, establecimiento_id, ficha_inspeccion_id,
                 evaluador_principal_id, estado, alcance, iniciada_en, finalizada_en,
                 porcentaje_cumplimiento, version_regla_riesgo_id, creado_por)
            VALUES
                 (@PreviousEvaluationId, @PreviousEvaluationNumber, @PreviousCaseId, @EstablishmentId,
                  @InspectionTemplateId, @EvaluatorId, 'EN_EJECUCION', 'COMPLETO',
                  CURRENT_TIMESTAMP, NULL, NULL,
                 @RiskRuleVersionId, @EvaluatorId),
                (@CurrentEvaluationId, @CurrentEvaluationNumber, @CurrentCaseId, @EstablishmentId,
                 @InspectionTemplateId, @EvaluatorId, 'EN_EJECUCION', 'SEGUIMIENTO',
                 CURRENT_TIMESTAMP, NULL, NULL, @RiskRuleVersionId, @EvaluatorId);

            INSERT INTO "SIGERSA"."RESPUESTA_USUARIO"
                (id, evaluacion_id, item_ficha_id, valor_texto, observacion,
                 nivel_criticidad_id, puntaje_obtenido, maximo_aplicable,
                 respondido_por, fecha_cliente, idempotency_key, creado_por)
             VALUES
                 (@PreviousResponseId, @PreviousEvaluationId, @ItemId, 'NO_CUMPLE',
                  'No conformidad temporal para prueba de integración.', @MajorCriticalityId,
                  0, 1, @EvaluatorId, CURRENT_TIMESTAMP, gen_random_uuid(), @EvaluatorId);

            UPDATE "SIGERSA"."EVALUACION"
               SET estado = 'FINALIZADA',
                   finalizada_en = CURRENT_TIMESTAMP + interval '1 minute',
                   porcentaje_cumplimiento = 75
             WHERE id = @PreviousEvaluationId;
            """, new
        {
            PreviousCaseId = previousCaseId,
            CurrentCaseId = currentCaseId,
            PreviousCaseNumber = $"CAS-SMOKE-P-{marker}",
            CurrentCaseNumber = $"CAS-SMOKE-C-{marker}",
            PreviousEvaluationId = previousEvaluationId,
            CurrentEvaluationId = currentEvaluationId,
            PreviousEvaluationNumber = $"EVA-SMOKE-P-{marker}",
            CurrentEvaluationNumber = $"EVA-SMOKE-C-{marker}",
            PreviousResponseId = previousResponseId,
            seed.CompanyId,
            seed.EstablishmentId,
            seed.ReasonId,
            seed.EvaluatorId,
            seed.InspectionTemplateId,
            seed.RiskRuleVersionId,
            seed.ItemId,
            seed.MajorCriticalityId
        });

        var repository = new EvaluationWorkflowRepository(new DbConnectionFactory(dataSource));
        var service = new EvaluationWorkflowService(repository);
        var workspace = await service.GetWorkspaceAsync(currentEvaluationId, seed.EvaluatorId, CancellationToken.None);
        if (workspace.Policy.Mode != InspectionQualificationPolicy.PreviousNonconformities ||
            workspace.Policy.RequiredSourceItems.Count != 1 ||
            workspace.Policy.ExcludedSourceItems.Count == 0)
            throw new InvalidOperationException("El alcance de seguimiento no redujo la ficha a la no conformidad anterior.");

        var supplement = await repository.SaveSupplementAsync(currentEvaluationId,
            new SaveEvaluationSupplementDraft(
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)), "75%",
                DateOnly.FromDateTime(DateTime.UtcNow), "En proceso",
                "Oficial de prueba", null, "Técnico de prueba", null,
                [new EvaluationFollowUpItem("Medida temporal", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)))],
                [], 0), seed.EvaluatorId, CancellationToken.None);
        supplement = await repository.SaveSupplementAsync(currentEvaluationId,
            new SaveEvaluationSupplementDraft(
                supplement.PreviousInspectionDate, supplement.PreviousQualification,
                supplement.CurrentInspectionDate, "Actualizada",
                supplement.DpsDasOfficer1, supplement.DpsDasOfficer2,
                supplement.DigemapsTechnician1, supplement.DigemapsTechnician2,
                supplement.CorrectiveMeasures, supplement.Recommendations,
                supplement.RowVersion), seed.EvaluatorId, CancellationToken.None);
        if (supplement.CurrentQualification != "Actualizada" || supplement.RowVersion < 2)
            throw new InvalidOperationException("Los datos complementarios no se insertaron y actualizaron correctamente.");

        var evidenceRepository = new EvidenceRepository(new DbConnectionFactory(dataSource));
        var evidence = new EvidenceRecord(
            Guid.NewGuid(), currentEvaluationId, seed.EvaluatorId, "SIGERSA_FILES",
            $"diagnostics/{evidenceIdempotencyKey:N}.png", "evidencia-smoke.png",
            $"{evidenceIdempotencyKey:N}.png", 8, "image/png", new string('a', 64),
            "FOTOGRAFIA", null, evidenceIdempotencyKey);
        var firstEvidenceId = await evidenceRepository.CreateAsync(evidence, CancellationToken.None);
        var repeatedEvidenceId = await evidenceRepository.CreateAsync(evidence, CancellationToken.None);
        if (firstEvidenceId != evidence.Id || repeatedEvidenceId != evidence.Id)
            throw new InvalidOperationException("La evidencia no se persistió de forma idempotente.");

        await service.SaveAnswerAsync(new SaveEvaluationAnswerDraft(
            currentEvaluationId,
            seed.SourceItem,
            "CUMPLE",
            null,
            "Corrección verificada en prueba de integración.",
            null,
            answerIdempotencyKey,
            Guid.NewGuid(),
            1,
            DateTimeOffset.UtcNow,
            null), seed.EvaluatorId, CancellationToken.None);
        var calculation = await service.CalculateAsync(
            currentEvaluationId, 1m, seed.EvaluatorId, CancellationToken.None);

        const string persistedEvaluationSql = @"
            SELECT COUNT(*)::integer AS SavedAnswers,
                   MAX(response.valor_texto) AS Rating,
                   evaluation.porcentaje_cumplimiento AS CompliancePercentage,
                   evaluation.snapshot_calculo -> 'Decision' ->> 'ApplicableItems' AS ApplicableItems
              FROM ""SIGERSA"".""EVALUACION"" evaluation
              LEFT JOIN ""SIGERSA"".""RESPUESTA_USUARIO"" response ON response.evaluacion_id = evaluation.id
             WHERE evaluation.id = @CurrentEvaluationId
             GROUP BY evaluation.porcentaje_cumplimiento, evaluation.snapshot_calculo;";
        var persisted = await connection.QuerySingleAsync<FocusedEvaluationResult>(
            persistedEvaluationSql,
            new { CurrentEvaluationId = currentEvaluationId });
        if (persisted.SavedAnswers != 1 || persisted.Rating != "CUMPLE" ||
            persisted.CompliancePercentage != 100m || calculation.Decision.ApplicableItems != 1 ||
            persisted.ApplicableItems != "1")
            throw new InvalidOperationException("La respuesta focalizada no quedó persistida o el cálculo incluyó ítems ocultos.");

        var correctionRepository = new CorrectionRepository(new DbConnectionFactory(dataSource));
        var correctionId = await correctionRepository.CreateAsync(new CorrectionDraft(
            correctionIdempotencyKey, currentEvaluationId, "TECNICO", seed.EvaluatorId,
            "Validar el flujo escalonado de corrección.", DateTimeOffset.UtcNow.AddDays(1), []),
            seed.EvaluatorId, CancellationToken.None);
        var administratorBeforeApproval = await correctionRepository.SearchAsync(new CorrectionSearch(
            marker.ToLowerInvariant(), null, 1, 10, seed.EvaluatorId, null, true, false,
            "ADMINISTRADOR"), CancellationToken.None);
        var coordinatorBeforeApproval = await correctionRepository.SearchAsync(new CorrectionSearch(
            marker.ToLowerInvariant(), null, 1, 10, seed.EvaluatorId, null, true, false,
            "COORDINADOR"), CancellationToken.None);
        if (administratorBeforeApproval.Items.Any(item => item.Id == correctionId)
            || coordinatorBeforeApproval.Items.All(item => item.Id != correctionId))
            throw new InvalidOperationException("La solicitud inicial no quedó visible exclusivamente para el coordinador.");
        var correctionVersion = await connection.ExecuteScalarAsync<long>("""
            SELECT version_fila FROM "SIGERSA"."CORRECCION" WHERE id = @CorrectionId;
            """, new { CorrectionId = correctionId });
        if (!await correctionRepository.ResolveAsync(correctionId, correctionVersion, "ACEPTADA",
                seed.EvaluatorId, true, CancellationToken.None))
            throw new InvalidOperationException("El coordinador no pudo enviar la corrección al administrador.");
        var coordinatorResult = await connection.QuerySingleAsync<CorrectionFlowResult>("""
            SELECT correction.estado AS CorrectionStatus, evaluation.estado AS EvaluationStatus,
                   correction.version_fila AS RowVersion
              FROM "SIGERSA"."CORRECCION" correction
              JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.id = correction.evaluacion_id
             WHERE correction.id = @CorrectionId;
            """, new { CorrectionId = correctionId });
        if (coordinatorResult.CorrectionStatus != "EN_PROCESO" || coordinatorResult.EvaluationStatus != "EN_CORRECCION")
            throw new InvalidOperationException("La aprobación del coordinador no mantuvo bloqueada la evaluación para el administrador.");
        var administratorAfterApproval = await correctionRepository.SearchAsync(new CorrectionSearch(
            marker.ToLowerInvariant(), null, 1, 10, seed.EvaluatorId, null, true, false,
            "ADMINISTRADOR"), CancellationToken.None);
        var coordinatorAfterApproval = await correctionRepository.SearchAsync(new CorrectionSearch(
            marker.ToLowerInvariant(), null, 1, 10, seed.EvaluatorId, null, true, false,
            "COORDINADOR"), CancellationToken.None);
        if (administratorAfterApproval.Items.All(item => item.Id != correctionId)
            || coordinatorAfterApproval.Items.Any(item => item.Id == correctionId))
            throw new InvalidOperationException("La solicitud aprobada por el coordinador no pasó exclusivamente al administrador.");
        if (!await correctionRepository.ResolveAsync(correctionId, coordinatorResult.RowVersion, "ACEPTADA",
                seed.EvaluatorId, false, CancellationToken.None))
            throw new InvalidOperationException("El administrador no pudo resolver la corrección.");
        var administratorResult = await connection.QuerySingleAsync<CorrectionFlowResult>("""
            SELECT correction.estado AS CorrectionStatus, evaluation.estado AS EvaluationStatus
              FROM "SIGERSA"."CORRECCION" correction
              JOIN "SIGERSA"."EVALUACION" evaluation ON evaluation.id = correction.evaluacion_id
             WHERE correction.id = @CorrectionId;
            """, new { CorrectionId = correctionId });
        if (administratorResult.CorrectionStatus != "ACEPTADA" || administratorResult.EvaluationStatus != "EN_EJECUCION")
            throw new InvalidOperationException("La decisión final del administrador no reanudó la evaluación.");

        var rejectedCorrectionId = await correctionRepository.CreateAsync(new CorrectionDraft(
            rejectedCorrectionIdempotencyKey, currentEvaluationId, "TECNICO", seed.EvaluatorId,
            "Validar el rechazo del coordinador.", DateTimeOffset.UtcNow.AddDays(1), []),
            seed.EvaluatorId, CancellationToken.None);
        var rejectedCorrectionVersion = await connection.ExecuteScalarAsync<long>("""
            SELECT version_fila FROM "SIGERSA"."CORRECCION" WHERE id = @CorrectionId;
            """, new { CorrectionId = rejectedCorrectionId });
        if (!await correctionRepository.ResolveAsync(rejectedCorrectionId, rejectedCorrectionVersion,
                "RECHAZADA", seed.EvaluatorId, true, CancellationToken.None))
            throw new InvalidOperationException("El coordinador no pudo rechazar la corrección.");
        var statusAfterCoordinatorRejection = await connection.ExecuteScalarAsync<string>("""
            SELECT estado FROM "SIGERSA"."EVALUACION" WHERE id = @CurrentEvaluationId;
            """, new { CurrentEvaluationId = currentEvaluationId });
        if (statusAfterCoordinatorRejection != "EN_EJECUCION")
            throw new InvalidOperationException("El rechazo del coordinador no desbloqueó la evaluación para el técnico.");

        await connection.ExecuteAsync("""
            UPDATE "SIGERSA"."EVALUACION"
               SET estado = 'EN_CORRECCION', modificado_en = CURRENT_TIMESTAMP,
                   modificado_por = @EvaluatorId, version_fila = version_fila + 1
             WHERE id = @CurrentEvaluationId;
            """, new { CurrentEvaluationId = currentEvaluationId, seed.EvaluatorId });
        var canUploadWhileCorrection = await new EvidenceRepository(new DbConnectionFactory(dataSource))
            .CanUploadAsync(seed.EvaluatorId, currentEvaluationId, CancellationToken.None);
        if (canUploadWhileCorrection)
            throw new InvalidOperationException("La evaluación en corrección permitió adjuntar evidencia.");
        try
        {
            await repository.SaveSupplementAsync(currentEvaluationId,
                new SaveEvaluationSupplementDraft(null, null, null, null, null, null, null, null, [], [],
                    supplement.RowVersion), seed.EvaluatorId, CancellationToken.None);
            throw new InvalidOperationException("La evaluación en corrección permitió modificar datos complementarios.");
        }
        catch (KeyNotFoundException)
        {
            // Estado esperado: el repositorio bloquea toda escritura mientras el coordinador revisa.
        }

        await connection.ExecuteAsync("""
            UPDATE "SIGERSA"."EVALUACION"
               SET estado = 'EN_EJECUCION', modificado_en = CURRENT_TIMESTAMP,
                   modificado_por = @EvaluatorId, version_fila = version_fila + 1
             WHERE id = @CurrentEvaluationId;
            """, new { CurrentEvaluationId = currentEvaluationId, seed.EvaluatorId });
        var finalized = await service.FinalizeAsync(currentEvaluationId, 1m,
            new EvaluationActor(seed.EvaluatorId, ["TECNICO_EVALUADOR"], null), CancellationToken.None);
        try
        {
            await service.TransitionAsync(currentEvaluationId,
                new EvaluationTransitionDraft("SUBMIT", finalized.RowVersion),
                new EvaluationActor(seed.AdministratorId, ["ADMINISTRADOR"], null), CancellationToken.None);
            throw new InvalidOperationException("El administrador pudo enviar una evaluación del técnico.");
        }
        catch (ForbiddenException)
        {
            // Estado esperado: enviar a revisión pertenece exclusivamente al técnico.
        }
        var submittedVersion = await service.TransitionAsync(currentEvaluationId,
            new EvaluationTransitionDraft("SUBMIT", finalized.RowVersion),
            new EvaluationActor(seed.EvaluatorId, ["TECNICO_EVALUADOR"], null), CancellationToken.None);
        await service.TransitionAsync(currentEvaluationId,
            new EvaluationTransitionDraft("APPROVE", submittedVersion),
            new EvaluationActor(seed.CoordinatorId, ["COORDINADOR"], null), CancellationToken.None);
        var finalEvaluationStatus = await connection.ExecuteScalarAsync<string>("""
            SELECT estado FROM "SIGERSA"."EVALUACION" WHERE id = @CurrentEvaluationId;
            """, new { CurrentEvaluationId = currentEvaluationId });
        if (finalEvaluationStatus != "APROBADA")
            throw new InvalidOperationException("El coordinador no pudo finalizar la evaluación enviada.");

        Console.WriteLine(
            "Focused evaluation write OK: visible=1, hidden={0}, saved={1}, calculated={2}, compliance={3}%, supplementVersion={4}, evidenceIdempotent=true, correctionReadOnly=true, evaluationFlow=technician-coordinator-final, correctionFlow=technician-coordinator-administrator",
            workspace.Policy.ExcludedSourceItems.Count,
            persisted.SavedAnswers,
            calculation.Decision.ApplicableItems,
            persisted.CompliancePercentage,
            supplement.RowVersion);
    }
    finally
    {
        const string cleanupSql = @"
            DELETE FROM ""SIGERSA"".""OPERACION_SINCRONIZACION""
             WHERE idempotency_key IN (@AnswerIdempotencyKey, @CorrectionIdempotencyKey,
                                       @RejectedCorrectionIdempotencyKey);
            DELETE FROM ""SIGERSA"".""CORRECCION_CAMPO""
             WHERE correccion_id IN (SELECT id FROM ""SIGERSA"".""CORRECCION""
                                      WHERE evaluacion_id = @CurrentEvaluationId);
            DELETE FROM ""SIGERSA"".""CORRECCION""
             WHERE evaluacion_id = @CurrentEvaluationId;
            DELETE FROM ""SIGERSA"".""RESPUESTA_USUARIO""
             WHERE evaluacion_id IN (@PreviousEvaluationId, @CurrentEvaluationId);
            DELETE FROM ""SIGERSA"".""EVIDENCIA""
             WHERE evaluacion_id = @CurrentEvaluationId;
            DELETE FROM ""SIGERSA"".""EVALUACION_COMPLEMENTO""
             WHERE evaluacion_id IN (@PreviousEvaluationId, @CurrentEvaluationId);
            DELETE FROM ""SIGERSA"".""EVALUACION""
             WHERE id IN (@PreviousEvaluationId, @CurrentEvaluationId);
            DELETE FROM ""SIGERSA"".""CASO""
             WHERE id IN (@PreviousCaseId, @CurrentCaseId);";
        await connection.ExecuteAsync(cleanupSql, new
        {
            AnswerIdempotencyKey = answerIdempotencyKey,
            CorrectionIdempotencyKey = correctionIdempotencyKey,
            RejectedCorrectionIdempotencyKey = rejectedCorrectionIdempotencyKey,
            PreviousEvaluationId = previousEvaluationId,
            CurrentEvaluationId = currentEvaluationId,
            PreviousCaseId = previousCaseId,
            CurrentCaseId = currentCaseId
        });
    }
}

sealed class FocusedEvaluationSeed
{
    public Guid EstablishmentId { get; init; }
    public Guid CompanyId { get; init; }
    public Guid InspectionTemplateId { get; init; }
    public Guid EvaluatorId { get; init; }
    public Guid CoordinatorId { get; init; }
    public Guid AdministratorId { get; init; }
    public Guid RiskRuleVersionId { get; init; }
    public Guid ItemId { get; init; }
    public int SourceItem { get; init; }
    public int TotalEvaluableItems { get; init; }
    public Guid ReasonId { get; init; }
    public Guid MajorCriticalityId { get; init; }
}

sealed class FocusedEvaluationResult
{
    public int SavedAnswers { get; init; }
    public string Rating { get; init; } = string.Empty;
    public decimal? CompliancePercentage { get; init; }
    public string? ApplicableItems { get; init; }
}

sealed class CorrectionFlowResult
{
    public string CorrectionStatus { get; init; } = string.Empty;
    public string EvaluationStatus { get; init; } = string.Empty;
    public long RowVersion { get; init; }
}
