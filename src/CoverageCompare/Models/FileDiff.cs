namespace CoverageCompare.Models;

/// <summary>
/// Per-file diff between Coverlet and MTP for files where the two tools disagree.
/// </summary>
internal sealed class FileDiff
{
    public required string FileName { get; init; }

    public int CoverletLinesValid { get; init; }
    public int CoverletLinesCovered { get; init; }
    public int MtpLinesValid { get; init; }
    public int MtpLinesCovered { get; init; }

    public double CoverletLineRate { get; init; }
    public double MtpLineRate { get; init; }

    /// <summary>MTP line rate minus Coverlet line rate. Positive = MTP covers more.</summary>
    public double LineDelta { get; init; }
}
