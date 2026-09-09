namespace SIGERSA.Domain.Entities;

public sealed record ParameterControl(
    long ParametersId,
    string KeyWord,
    int? CompanyCode,
    int? OCode,
    string? CCode,
    int? NumericData,
    double? DoubleData,
    string? StringData,
    bool? BooleanData,
    DateTimeOffset? DateData,
    bool Status,
    string CUser,
    DateTimeOffset CDate,
    string? MUser,
    DateTimeOffset? MDate,
    string? DUser,
    DateTimeOffset? DDate);

public sealed record ParameterControlDraft(
    string KeyWord,
    int? CompanyCode,
    int? OCode,
    string? CCode,
    int? NumericData,
    double? DoubleData,
    string? StringData,
    bool? BooleanData,
    DateTimeOffset? DateData);
