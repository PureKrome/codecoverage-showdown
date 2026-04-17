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

        Console.WriteLine(
            $"[Compare] {label}: line={lineRate:P1} branch={branchRate:P1} method={methodRate:P1} packages={packages.Count}");

        return new CoberturaReport
        {
            Summary = new SummaryStats(lineRate, branchRate, methodRate),
            Packages = packages,
        };
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
}
