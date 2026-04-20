using CoverageCompare.Models;

namespace CoverageCompare;

/// <summary>
/// Builds the <see cref="OutputFile"/> and updated <see cref="HistoryFile"/>
/// from two parsed Cobertura reports.
/// </summary>
internal static class ReportBuilder
{
    private const int MaxHistoryEntries = 90;
    private const int MaxFileDiffs = 10;

    public static (OutputFile Output, HistoryFile History) Build(
        CoberturaReport coverletReport,
        CoberturaReport mteccReport,
        string coverletVersion,
        string mteccVersion,
        string humanizerSha,
        HistoryFile existingHistory)
    {
        var runEntry = new RunEntry
        {
            RunAt = DateTime.UtcNow.ToString("o"),
            HumanizerSha = humanizerSha,
            Coverlet = new ToolSummary
            {
                Version = coverletVersion,
                LineRate = coverletReport.Summary.LineRate,
                BranchRate = coverletReport.Summary.BranchRate,
                MethodRate = coverletReport.Summary.MethodRate,
            },
            Mtecc = new ToolSummary
            {
                Version = mteccVersion,
                LineRate = mteccReport.Summary.LineRate,
                BranchRate = mteccReport.Summary.BranchRate,
                MethodRate = mteccReport.Summary.MethodRate,
            },
        };

        // Compute deltas (Coverlet - MTECC) and round for stable reporting
        runEntry.LineDelta = Math.Round(runEntry.Coverlet.LineRate - runEntry.Mtecc.LineRate, 4);
        runEntry.BranchDelta = Math.Round(runEntry.Coverlet.BranchRate - runEntry.Mtecc.BranchRate, 4);
        runEntry.MethodDelta = Math.Round(runEntry.Coverlet.MethodRate - runEntry.Mtecc.MethodRate, 4);

        // Prepend newest first, cap at max.
        var runs = new List<RunEntry>(existingHistory.Runs.Count + 1) { runEntry };
        runs.AddRange(existingHistory.Runs);
        if (runs.Count > MaxHistoryEntries)
        {
            runs = runs[..MaxHistoryEntries];
        }

        var output = new OutputFile
        {
            Latest = runEntry,
            PackageComparisons = BuildPackageComparisons(coverletReport, mteccReport),
            FileDiffs = BuildFileDiffs(coverletReport, mteccReport),
        };

        var history = new HistoryFile { Runs = runs };

        return (output, history);
    }

    private static List<PackageComparison> BuildPackageComparisons(
        CoberturaReport coverletReport,
        CoberturaReport mteccReport)
    {
        var coverletPkgs = coverletReport.Packages.ToDictionary(p => p.Name, StringComparer.Ordinal);
        var mteccPkgs = mteccReport.Packages.ToDictionary(p => p.Name, StringComparer.Ordinal);
        var allPkgNames = coverletPkgs.Keys.Union(mteccPkgs.Keys).OrderBy(x => x).ToList();

        return allPkgNames.Select(name =>
        {
            coverletPkgs.TryGetValue(name, out var cv);
            mteccPkgs.TryGetValue(name, out var mv);

                return new PackageComparison
                {
                    Name = name,
                    CoverletLine = cv?.LineRate,
                    CoverletBranch = cv?.BranchRate,
                    MteccLine = mv?.LineRate,
                    MteccBranch = mv?.BranchRate,
                };
        }).ToList();
    }

    /// <summary>
    /// Aligns files from both reports by normalised filename, computes the line rate
    /// delta for each file that appears in both, and returns the top <see cref="MaxFileDiffs"/>
    /// ordered by absolute delta descending (biggest disagreement first).
    /// Files that only appear in one tool are excluded — they are likely instrumentation
    /// scope differences rather than meaningful coverage gaps.
    /// </summary>
    private static List<FileDiff> BuildFileDiffs(
        CoberturaReport coverletReport,
        CoberturaReport mteccReport)
    {
        var coverletFiles = coverletReport.Files.ToDictionary(f => f.FileName, StringComparer.OrdinalIgnoreCase);
        var mteccFiles = mteccReport.Files.ToDictionary(f => f.FileName, StringComparer.OrdinalIgnoreCase);

        var commonFiles = coverletFiles.Keys.Intersect(mteccFiles.Keys, StringComparer.OrdinalIgnoreCase);

        return commonFiles
            .Select(name =>
            {
                var cv = coverletFiles[name];
                var mv = mteccFiles[name];
                var delta = Math.Round(mv.LineRate - cv.LineRate, 4);

                return new FileDiff
                {
                    FileName = name,
                    CoverletLinesValid = cv.LinesValid,
                    CoverletLinesCovered = cv.LinesCovered,
                    MteccLinesValid = mv.LinesValid,
                    MteccLinesCovered = mv.LinesCovered,
                    CoverletLineRate = cv.LineRate,
                    MteccLineRate = mv.LineRate,
                    LineDelta = delta,
                };
            })
            .Where(d => Math.Abs(d.LineDelta) > 0.0)
            .OrderByDescending(d => Math.Abs(d.LineDelta))
            .Take(MaxFileDiffs)
            .ToList();
    }
}
