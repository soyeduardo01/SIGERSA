using Npgsql;
using SIGERSA.Domain.Entities;
using SIGERSA.Infrastructure.Persistence;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var connectionLine = File.ReadLines(Path.Combine(root, ".env.local"))
    .First(line => line.StartsWith("Database__ConnectionString=", StringComparison.Ordinal));
var connectionString = connectionLine[(connectionLine.IndexOf('=') + 1)..].Trim().Trim('"');
await using var dataSource = NpgsqlDataSource.Create(connectionString);
var factory = new DbConnectionFactory(dataSource);
var actorId = Guid.Empty;

var requests = await new InspectionRequestRepository(factory).SearchAsync(
    new InspectionRequestSearch(null, null, 1, 5, actorId, null, true, false));
var cases = await new CaseRepository(factory).SearchAsync(
    new CaseSearch(null, null, 1, 5, actorId, null, true, false));
var schedules = await new SchedulingRepository(factory).SearchAsync(new ScheduleSearch(null, null, 1, 5));
var evaluations = await new EvaluationWorkflowRepository(factory).SearchAsync(
    new EvaluationSearch(null, null, 1, 5, actorId, null, true, false));
var evidences = await new EvidenceRepository(factory).SearchAsync(
    new EvidenceSearch(null, null, 1, 5, actorId, null, true, false));
var corrections = await new CorrectionRepository(factory).SearchAsync(
    new CorrectionSearch(null, null, 1, 5, actorId, null, true, false));

Console.WriteLine($"SQL smoke OK: requests={requests.Total}, cases={cases.Total}, schedules={schedules.Total}, evaluations={evaluations.Total}, evidences={evidences.Total}, corrections={corrections.Total}");
