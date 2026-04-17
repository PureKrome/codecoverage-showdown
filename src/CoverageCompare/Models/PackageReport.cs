namespace CoverageCompare.Models;

internal record PackageReport
{
    public required string Name { get; init; }
    public required double LineRate { get; init; }
    public required double BranchRate { get; init; }
    public required double MethodRate { get; init; }
}
