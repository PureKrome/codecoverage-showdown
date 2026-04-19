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
        CoberturaReport mtpReport,
        string coverletVersion,
        string mtpVersion,
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
            Mtp = new ToolSummary
            {
                Version = mtpVersion,
                LineRate = mtpReport.Summary.LineRate,
                BranchRate = mtpReport.Summary.BranchRate,
                MethodRate = mtpReport.Summary.MethodRate,
            },
        };

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
            PackageComparisons = BuildPackageComparisons(coverletReport, mtpReport),
            FileDiffs = BuildFileDiffs(coverletReport, mtpReport),
        };

        var history = new HistoryFile { Runs = runs };

        return (output, history);
    }

    private static List<PackageComparison> BuildPackageComparisons(
        CoberturaReport coverletReport,
        CoberturaReport mtpReport)
    {
        var coverletPkgs = coverletReport.Packages.ToDictionary(p => p.Name, StringComparer.Ordinal);
        var mtpPkgs = mtpReport.Packages.ToDictionary(p => p.Name, StringComparer.Ordinal);
        var allPkgNames = coverletPkgs.Keys.Union(mtpPkgs.Keys).OrderBy(x => x).ToList();

        return allPkgNames.Select(name =>
        {
            coverletPkgs.TryGetValue(name, out var cv);
            mtpPkgs.TryGetValue(name, out var mv);

            return new PackageComparison
            {
                Name = name,
                CoverletLine = cv?.LineRate,
                CoverletBranch = cv?.BranchRate,
                MtpLine = mv?.LineRate,
                MtpBranch = mv?.BranchRate,
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
        CoberturaReport mtpReport)
    {
        var coverletFiles = coverletReport.Files.ToDictionary(f => f.FileName, StringComparer.OrdinalIgnoreCase);
        var mtpFiles = mtpReport.Files.ToDictionary(f => f.FileName, StringComparer.OrdinalIgnoreCase);

        var commonFiles = coverletFiles.Keys.Intersect(mtpFiles.Keys, StringComparer.OrdinalIgnoreCase);

        return commonFiles
            .Select(name =>
            {
                var cv = coverletFiles[name];
                var mv = mtpFiles[name];
                var delta = Math.Round(mv.LineRate - cv.LineRate, 4);

                return new FileDiff
                {
                    FileName = name,
                    CoverletLinesValid = cv.LinesValid,
                    CoverletLinesCovered = cv.LinesCovered,
                    MtpLinesValid = mv.LinesValid,
                    MtpLinesCovered = mv.LinesCovered,
                    CoverletLineRate = cv.LineRate,
                    MtpLineRate = mv.LineRate,
                    LineDelta = delta,
                };
            })
            .Where(d => Math.Abs(d.LineDelta) > 0.0)
            .OrderByDescending(d => Math.Abs(d.LineDelta))
            .Take(MaxFileDiffs)
            .ToList();
    }
}
