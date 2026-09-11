using Dapper;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Infrastructure.Persistence;

public sealed class ParametersControlRepository(IDbConnectionFactory connectionFactory)
    : DapperRepositoryBase(connectionFactory), IParametersControlRepository
{
    public async Task<IReadOnlyList<ParameterControl>> GetAllActiveAsync(string? search, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "ParametersId", "KeyWord", "CompanyCode", "OCode", "CCode",
                "NumericData", "DoubleData", "StringData", "BooleanData", "DateData",
                "Status", "CUser", "CDate", "MUser", "MDate", "DUser", "DDate"
            FROM "SIGERSA"."ParametersControl"
            WHERE "Status" = true
              AND (@Search IS NULL
                OR "KeyWord" ILIKE '%' || @Search || '%'
                OR COALESCE("CCode", '') ILIKE '%' || @Search || '%'
                OR COALESCE("StringData", '') ILIKE '%' || @Search || '%')
            ORDER BY "KeyWord", "NumericData" NULLS LAST, "StringData" NULLS LAST, "ParametersId";
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var command = new CommandDefinition(Sql(sql), new { Search = search }, cancellationToken: cancellationToken);
            var rows = await connection.QueryAsync<ParameterControlRow>(command);
            return rows.Select(row => row.ToDomain()).ToArray();
        }
    }

    public async Task<IReadOnlyList<ParameterControl>> GetActiveAsync(string keyWord, int? companyCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT *
            FROM "SIGERSA"."FN_ParametersControl_GetActive"(@KeyWord, @CompanyCode);
            """;
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var command = new CommandDefinition(Sql(sql), new { KeyWord = keyWord, CompanyCode = companyCode }, cancellationToken: cancellationToken);
            var rows = await connection.QueryAsync<ParameterControlRow>(command);
            return rows.Select(row => row.ToDomain()).ToArray();
        }
    }

    public Task<long> CreateAsync(ParameterControlDraft parameter, string user, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "SIGERSA"."FN_ParametersControl_Create"(
                @KeyWord, @CompanyCode, @OCode, @CCode, @NumericData,
                @DoubleData, @StringData, @BooleanData, @DateData, @User);
            """;
        return ExecuteScalarAsync<long>(sql, ToParameters(parameter, user), cancellationToken);
    }

    public Task<bool> UpdateAsync(long parametersId, ParameterControlDraft parameter, string user, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "SIGERSA"."FN_ParametersControl_Update"(
                @ParametersId, @KeyWord, @CompanyCode, @OCode, @CCode,
                @NumericData, @DoubleData, @StringData, @BooleanData, @DateData, @User);
            """;
        var values = new DynamicParameters(ToParameters(parameter, user));
        values.Add("ParametersId", parametersId);
        return ExecuteScalarAsync<bool>(sql, values, cancellationToken);
    }

    public Task<bool> SoftDeleteAsync(long parametersId, string user, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "SIGERSA"."FN_ParametersControl_SoftDelete"(@ParametersId, @User);
            """;
        return ExecuteScalarAsync<bool>(sql, new { ParametersId = parametersId, User = user }, cancellationToken);
    }

    private async Task<T> ExecuteScalarAsync<T>(string sql, object parameters, CancellationToken cancellationToken)
    {
        var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            var command = new CommandDefinition(Sql(sql), parameters, cancellationToken: cancellationToken);
            return (await connection.ExecuteScalarAsync<T>(command))!;
        }
    }

    private static object ToParameters(ParameterControlDraft parameter, string user) => new
    {
        parameter.KeyWord,
        parameter.CompanyCode,
        parameter.OCode,
        parameter.CCode,
        parameter.NumericData,
        parameter.DoubleData,
        parameter.StringData,
        parameter.BooleanData,
        parameter.DateData,
        User = user
    };

    private sealed class ParameterControlRow
    {
        public long ParametersId { get; init; }
        public string KeyWord { get; init; } = string.Empty;
        public int? CompanyCode { get; init; }
        public int? OCode { get; init; }
        public string? CCode { get; init; }
        public int? NumericData { get; init; }
        public double? DoubleData { get; init; }
        public string? StringData { get; init; }
        public bool? BooleanData { get; init; }
        public DateTime? DateData { get; init; }
        public bool Status { get; init; }
        public string CUser { get; init; } = string.Empty;
        public DateTime CDate { get; init; }
        public string? MUser { get; init; }
        public DateTime? MDate { get; init; }
        public string? DUser { get; init; }
        public DateTime? DDate { get; init; }

        public ParameterControl ToDomain() => new(
            ParametersId, KeyWord, CompanyCode, OCode, CCode, NumericData,
            DoubleData, StringData, BooleanData, Utc(DateData), Status, CUser,
            Utc(CDate), MUser, Utc(MDate), DUser, Utc(DDate));

        private static DateTimeOffset Utc(DateTime value) =>
            new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

        private static DateTimeOffset? Utc(DateTime? value) =>
            value is null ? null : Utc(value.Value);
    }
}
