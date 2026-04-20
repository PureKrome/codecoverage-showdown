namespace CoverageCompare.Models;

internal sealed class PackageComparison
{
    public string Name { get; set; } = "";
    public double? CoverletLine { get; set; }
    public double? CoverletBranch { get; set; }
    public double? MteccLine { get; set; }
    public double? MteccBranch { get; set; }
}
