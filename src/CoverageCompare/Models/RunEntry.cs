namespace CoverageCompare.Models;

internal sealed class RunEntry
{
    public string RunAt { get; set; } = "";
    public string HumanizerSha { get; set; } = "";
    public ToolSummary Coverlet { get; set; } = new();
    public ToolSummary Mtecc { get; set; } = new();

    // Per-run deltas: Coverlet - MTECC
    // Positive value means Coverlet reports higher coverage than MTECC
    public double LineDelta { get; set; }
    public double BranchDelta { get; set; }
    public double MethodDelta { get; set; }
}
