namespace CoverageCompare.Models;

internal sealed class RunEntry
{
    public string RunAt { get; set; } = "";
    public string HumanizerSha { get; set; } = "";
    public ToolSummary Coverlet { get; set; } = new();
    public ToolSummary Mtp { get; set; } = new();
}
