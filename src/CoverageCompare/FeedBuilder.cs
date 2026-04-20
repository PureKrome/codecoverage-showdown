using System.Xml.Linq;
using CoverageCompare.Models;

namespace CoverageCompare;

/// <summary>
/// Generates an Atom 1.0 feed from the run history.
/// Atom is supported by all RSS readers and is more precisely specified than RSS 2.0.
/// </summary>
internal static class FeedBuilder
{
    private const string SiteUrl = "https://purekrome.github.io/codecoverage-showdown";
    private const string FeedUrl = $"{SiteUrl}/feed.xml";

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public static XDocument Build(HistoryFile history)
    {
        var updated = history.Runs.Count > 0
            ? history.Runs[0].RunAt  // newest first
            : DateTime.UtcNow.ToString("o");

        var feed = new XElement(Atom + "feed",
            new XElement(Atom + "id",      FeedUrl),
            new XElement(Atom + "title",   "Coverage Showdown — Coverlet vs MTECC"),
            new XElement(Atom + "subtitle","Nightly .NET code coverage comparison against Humanizer"),
            new XElement(Atom + "link",    new XAttribute("rel", "alternate"), new XAttribute("href", SiteUrl)),
            new XElement(Atom + "link",    new XAttribute("rel", "self"),      new XAttribute("href", FeedUrl)),
            new XElement(Atom + "updated", updated),
            new XElement(Atom + "author",
                new XElement(Atom + "name", "Coverage Showdown")));

        foreach (var run in history.Runs)
        {
            feed.Add(BuildEntry(run));
        }

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            feed);
    }

    private static XElement BuildEntry(RunEntry run)
    {
        var entryId = $"{SiteUrl}#{run.RunAt}";
            var title   = $"coverlet {run.Coverlet.Version} / MTECC {run.Mtecc.Version} — Humanizer {run.HumanizerSha}";

        var content = $"""
            <table>
              <tr><th></th><th>Coverlet {run.Coverlet.Version}</th><th>MTECC {run.Mtecc.Version}</th><th>Delta</th></tr>
              <tr><td>Line</td><td>{run.Coverlet.LineRate:P1}</td><td>{run.Mtecc.LineRate:P1}</td><td>{Delta(run.Mtecc.LineRate, run.Coverlet.LineRate)}</td></tr>
              <tr><td>Branch</td><td>{run.Coverlet.BranchRate:P1}</td><td>{run.Mtecc.BranchRate:P1}</td><td>{Delta(run.Mtecc.BranchRate, run.Coverlet.BranchRate)}</td></tr>
              <tr><td>Method</td><td>{run.Coverlet.MethodRate:P1}</td><td>{run.Mtecc.MethodRate:P1}</td><td>{Delta(run.Mtecc.MethodRate, run.Coverlet.MethodRate)}</td></tr>
            </table>
            <p>Humanizer commit: {run.HumanizerSha} &bull; <a href="{SiteUrl}">View full report</a></p>
            """;

        return new XElement(Atom + "entry",
            new XElement(Atom + "id",      entryId),
            new XElement(Atom + "title",   title),
            new XElement(Atom + "updated", run.RunAt),
            new XElement(Atom + "link",    new XAttribute("href", SiteUrl)),
            new XElement(Atom + "content", new XAttribute("type", "html"), content));
    }

    private static string Delta(double b, double a)
    {
        var d = (b - a) * 100;
        return d >= 0 ? $"+{d:F2}pp" : $"{d:F2}pp";
    }
}
