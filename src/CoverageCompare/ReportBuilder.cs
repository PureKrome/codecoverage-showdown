using CoverageCompare.Models;

namespace CoverageCompare;

/// <summary>
/// Builds the <see cref="OutputFile"/> and updated <see cref="HistoryFile"/>
/// from two parsed Cobertura reports.
/// </summary>
internal static class ReportBuilder
{
    private const int MaxHistoryEntries = 90;

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
}
