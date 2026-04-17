namespace CoverageCompare.Models;

internal record CoberturaReport
{
    public required SummaryStats Summary { get; init; }
    public required List<PackageReport> Packages { get; init; }
}
