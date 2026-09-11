using Npgsql;
using SIGERSA.Domain.Entities;
using SIGERSA.Infrastructure.Persistence;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var connectionLine = File.ReadLines(Path.Combine(root, ".env.local"))
    .First(line => line.StartsWith("Database__ConnectionString=", StringComparison.Ordinal));
var connectionString = connectionLine[(connectionLine.IndexOf('=') + 1)..].Trim().Trim('"');
await using var dataSource = NpgsqlDataSource.Create(connectionString);

if (args.Contains("--migrate-latest", StringComparer.Ordinal))
{
    foreach (var fileName in new[]
    {
        "013_complete_functional_support.sql",
        "014_two_factor_authentication.sql",
        "015_request_pending_assignment_status.sql",
        "016_public_user_registration.sql",
        "017_establishment_catalogs_and_generated_code.sql"
    })
    {
        var migrationPath = Path.Combine(root, "src", "backend", "Database", "Migrations", fileName);
        await using var migrationCommand = dataSource.CreateCommand(await File.ReadAllTextAsync(migrationPath));
        migrationCommand.CommandTimeout = 120;
        await migrationCommand.ExecuteNonQueryAsync();
        Console.WriteLine($"Migration {Path.GetFileNameWithoutExtension(fileName)} applied successfully.");
    }
}

var factory = new DbConnectionFactory(dataSource);
var actorId = Guid.Empty;

var requests = await new InspectionRequestRepository(factory).SearchAsync(
    new InspectionRequestSearch(null, null, 1, 5, actorId, null, true, false));
var cases = await new CaseRepository(factory).SearchAsync(
    new CaseSearch(null, null, 1, 5, actorId, null, true, false));
var schedules = await new SchedulingRepository(factory).SearchAsync(
    new ScheduleSearch(null, null, 1, 5, actorId, false));
var evaluations = await new EvaluationWorkflowRepository(factory).SearchAsync(
    new EvaluationSearch(null, null, 1, 5, actorId, null, true, false));
var evaluationOptions = await new EvaluationWorkflowRepository(factory).GetOptionsAsync(true);
var establishmentOptions = await new EstablishmentRepository(factory).GetOptionsAsync();
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
var history = await operations.SearchHistoryAsync(
    new HistoricalEvaluationSearch(null, null, null, null, 1, 5, operationalScope));
var audit = await operations.SearchAuditAsync(new AuditEventSearch(null, null, null, null, 1, 5));

Console.WriteLine($"SQL smoke OK: requests={requests.Total}, cases={cases.Total}, schedules={schedules.Total}, evaluations={evaluations.Total}, evaluationOptions={evaluationOptions.Cases.Count + evaluationOptions.Establishments.Count + evaluationOptions.Templates.Count + evaluationOptions.RiskRules.Count + evaluationOptions.Evaluators.Count}, establishmentCatalogs={establishmentOptions.Markets.Count + establishmentOptions.Commercializations.Count + establishmentOptions.HaccpLevels.Count + establishmentOptions.SamplingApplications.Count + establishmentOptions.InabieDistributions.Count}, evidences={evidences.Total}, corrections={corrections.Total}, companies={companies.Total}, dashboard={dashboard.ActiveCases}, surveillance={surveillance.Total}, findings={findings.Total}, history={history.Total}, audit={audit.Total}, activation={canActivateUser}, userDocument={canAttachUserDocument}, requestDocument={canAttachRequestDocument}");

static void AssertCatalog(IEnumerable<string> actual, string[] expected, string name)
{
    var actualValues = actual.ToHashSet(StringComparer.Ordinal);
    if (expected.Any(value => !actualValues.Contains(value)))
        throw new InvalidOperationException($"El catálogo de {name} no contiene todos los valores esperados.");
}
