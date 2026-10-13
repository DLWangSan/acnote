namespace AcNote.App;

public sealed record ImportFileSummary(
    string Path,
    int Added,
    int Updated,
    IReadOnlyList<string> Warnings,
    string? Error);

public sealed record ImportSummary(
    int Added,
    int Updated,
    int LibraryCount,
    IReadOnlyList<ImportFileSummary> Files);
