using System.Globalization;
using System.Xml.Linq;
using CoverageCompare.Models;

namespace CoverageCompare;

internal static class CoberturaParser
{
    public static CoberturaReport Parse(string path, string label)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"[Compare] {label} XML not found: {path}");
        }

        var doc = XDocument.Load(path);
        var coverage = doc.Root!;

        var lineRate = ParseRate(coverage.Attribute("line-rate"));
        var branchRate = ParseRate(coverage.Attribute("branch-rate"));

        // Method rate is not a top-level attribute in Cobertura — derive it
        // from the method-level line-rate aggregation across all methods.
        var allMethods = coverage
            .Descendants("method")
            .Select(m => ParseRate(m.Attribute("line-rate")))
            .ToList();

        var methodRate = allMethods.Count > 0
            ? allMethods.Average()
            : 0.0;

        var packages = coverage
            .Descendants("package")
            .Select(pkg => new PackageReport
            {
                Name = (string?)pkg.Attribute("name") ?? "unknown",
                LineRate = ParseRate(pkg.Attribute("line-rate")),
                BranchRate = ParseRate(pkg.Attribute("branch-rate")),
                MethodRate = pkg.Descendants("method")
                    .Select(m => ParseRate(m.Attribute("line-rate")))
                    .DefaultIfEmpty(0.0)
                    .Average(),
            })
            .ToList();

        var files = ParseFiles(coverage);

        Console.WriteLine(
            $"[Compare] {label}: line={lineRate:P1} branch={branchRate:P1} method={methodRate:P1} " +
            $"packages={packages.Count} files={files.Count}");

        return new CoberturaReport
        {
            Summary = new SummaryStats(lineRate, branchRate, methodRate),
            Packages = packages,
            Files = files,
        };
    }

    /// <summary>
    /// Aggregates per-file stats from all <c>&lt;class&gt;</c> elements.
    /// Multiple classes can share the same filename (partial classes, nested types),
    /// so we group and sum the raw line/branch counts rather than averaging rates.
    /// The filename is normalised to a forward-slash relative path by stripping
    /// any leading path components up to and including "src/" so that results from
    /// two different clone directories can be aligned.
    /// </summary>
    private static List<FileReport> ParseFiles(XElement coverage)
    {
        // accumulator: filename -> (linesValid, linesCovered, branchesValid, branchesCovered)
        var acc = new Dictionary<string, (int lv, int lc, int bv, int bc)>(StringComparer.OrdinalIgnoreCase);

        foreach (var cls in coverage.Descendants("class"))
        {
            var rawFile = (string?)cls.Attribute("filename");
            if (string.IsNullOrWhiteSpace(rawFile))
            {
                continue;
            }

            var fileName = NormaliseFileName(rawFile);

            var lines = cls.Descendants("line").ToList();
            var linesValid = lines.Count;
            var linesCovered = lines.Count(l => ParseInt(l.Attribute("hits")) > 0);

            var branchLines = lines.Where(l => string.Equals(
                (string?)l.Attribute("branch"), "True", StringComparison.OrdinalIgnoreCase)).ToList();

            var branchesValid = 0;
            var branchesCovered = 0;
            foreach (var bl in branchLines)
            {
                // condition-coverage="50% (1/2)" — parse the (covered/total) part
                var condAttr = (string?)bl.Attribute("condition-coverage");
                if (condAttr is not null && TryParseConditionCoverage(condAttr, out var cv, out var ct))
                {
                    branchesValid += ct;
                    branchesCovered += cv;
                }
                else
                {
                    // fallback: count as one branch, covered if hits > 0
                    branchesValid += 1;
                    branchesCovered += ParseInt(bl.Attribute("hits")) > 0 ? 1 : 0;
                }
            }

            if (acc.TryGetValue(fileName, out var existing))
            {
                acc[fileName] = (
                    existing.lv + linesValid,
                    existing.lc + linesCovered,
                    existing.bv + branchesValid,
                    existing.bc + branchesCovered);
            }
            else
            {
                acc[fileName] = (linesValid, linesCovered, branchesValid, branchesCovered);
            }
        }

        return acc
            .Select(kvp => new FileReport
            {
                FileName = kvp.Key,
                LinesValid = kvp.Value.lv,
                LinesCovered = kvp.Value.lc,
                BranchesValid = kvp.Value.bv,
                BranchesCovered = kvp.Value.bc,
            })
            .ToList();
    }

    /// <summary>
    /// Strips absolute path prefix, keeping only the part from "src/" onward.
    /// e.g. "/tmp/clone/src/Humanizer/Foo.cs" → "src/Humanizer/Foo.cs"
    /// Falls back to a relative path (preserving any directory segments) if "src/" is not found.
    /// </summary>
    private static string NormaliseFileName(string raw)
    {
        var normalised = raw.Replace('\\', '/').Trim();

        // Prefer returning the path relative to the Humanizer source tree when available.
        // e.g. "/.../src/Humanizer/Bytes/ByteRate.cs" -> "Bytes/ByteRate.cs"
        var marker = "src/Humanizer/";
        var idx = normalised.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return normalised[(idx + marker.Length)..].TrimStart('/');
        }

        // If src/Humanizer isn't present, but src/ is, strip the leading src/ and keep the
        // remainder (e.g. ".../src/Some/Path.cs" -> "Some/Path.cs").
        var srcIdx = normalised.IndexOf("src/", StringComparison.OrdinalIgnoreCase);
        if (srcIdx >= 0)
        {
            return normalised[(srcIdx + "src/".Length)..].TrimStart('/');
        }

        // Otherwise return the path as-is (preserving directory segments) so
        // "Bytes/ByteRate.cs" stays "Bytes/ByteRate.cs" rather than just "ByteRate.cs".
        return normalised.TrimStart('/');
    }

    /// <summary>Parses "50% (1/2)" → covered=1, total=2.</summary>
    private static bool TryParseConditionCoverage(string value, out int covered, out int total)
    {
        covered = total = 0;
        var open = value.IndexOf('(');
        var slash = value.IndexOf('/');
        var close = value.IndexOf(')');
        if (open < 0 || slash < 0 || close < 0 || slash < open || close < slash)
        {
            return false;
        }

        return int.TryParse(value[(open + 1)..slash].Trim(), out covered)
            && int.TryParse(value[(slash + 1)..close].Trim(), out total);
    }

    private static double ParseRate(XAttribute? attr)
    {
        if (attr is null)
        {
            return 0.0;
        }

        return double.TryParse(attr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? Math.Round(v, 4)
            : 0.0;
    }

    private static int ParseInt(XAttribute? attr)
    {
        if (attr is null)
        {
            return 0;
        }

        return int.TryParse(attr.Value, out var v) ? v : 0;
    }
}
