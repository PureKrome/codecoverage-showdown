namespace CoverageCompare.Models;

internal sealed class PackageComparison
{
    public string Name { get; set; } = "";
    public double? CoverletLine { get; set; }
    public double? CoverletBranch { get; set; }
    public double? MtpLine { get; set; }
    public double? MtpBranch { get; set; }
}
