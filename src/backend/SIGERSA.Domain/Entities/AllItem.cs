namespace SIGERSA.Domain.Entities;

public sealed record AllItem(
    int Items,
    string ItemsId,
    string Description,
    string SectionType,
    string? Parents);

public sealed record AllItemDraft(
    string ItemsId,
    string Description,
    string SectionType,
    string? Parents);
