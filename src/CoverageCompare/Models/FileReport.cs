namespace CoverageCompare.Models;

/// <summary>
/// Per-file coverage stats parsed from a single Cobertura report.
/// A file may appear in multiple <c>&lt;class&gt;</c> elements (one per partial class /
/// nested type); stats here are the aggregate across all of them.
/// </summary>
internal sealed class FileReport
{
    /// <summary>Relative path as it appears in the Cobertura XML filename attribute.</summary>
    public required string FileName { get; init; }

    public int LinesValid { get; init; }
    public int LinesCovered { get; init; }
    public int BranchesValid { get; init; }
    public int BranchesCovered { get; init; }

    public double LineRate => LinesValid == 0 ? 0.0 : Math.Round((double)LinesCovered / LinesValid, 4);
    public double BranchRate => BranchesValid == 0 ? 0.0 : Math.Round((double)BranchesCovered / BranchesValid, 4);
}
