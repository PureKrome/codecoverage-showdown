namespace CoverageCompare.Models;

internal sealed class OutputFile
{
    public RunEntry Latest { get; set; } = new();
    public List<PackageComparison> PackageComparisons { get; set; } = [];
}
