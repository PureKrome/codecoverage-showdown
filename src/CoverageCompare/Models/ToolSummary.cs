namespace CoverageCompare.Models;

internal sealed class ToolSummary
{
    public string Version { get; set; } = "";
    public double LineRate { get; set; }
    public double BranchRate { get; set; }
    public double MethodRate { get; set; }
}
