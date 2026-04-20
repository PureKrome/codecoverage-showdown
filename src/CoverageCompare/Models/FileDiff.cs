namespace CoverageCompare.Models;

/// <summary>
/// Per-file diff between Coverlet and MTECC for files where the two tools disagree.
/// </summary>
internal sealed class FileDiff
{
    public required string FileName { get; init; }

    public int CoverletLinesValid { get; init; }
    public int CoverletLinesCovered { get; init; }
    public int MteccLinesValid { get; init; }
    public int MteccLinesCovered { get; init; }

    public double CoverletLineRate { get; init; }
    public double MteccLineRate { get; init; }

    /// <summary>MTECC line rate minus Coverlet line rate. Positive = MTECC covers more.</summary>
    public double LineDelta { get; init; }
}
