using System.Text.Json;
using System.Text.Json.Serialization;
using CoverageCompare;
using CoverageCompare.Models;

// Args:
//   <coverlet-cobertura.xml>
  //   <mtecc-cobertura.xml>
//   <output-latest.json>
//   <output-history.json>
//   <output-feed.xml>
//   <coverlet-version>
  //   <mtecc-version>
//   <humanizer-sha>

if (args.Length < 8)
{
    Console.Error.WriteLine(
        "Usage: CoverageCompare <coverlet.xml> <mtecc.xml> <latest.json> <history.json> <feed.xml> " +
        "<coverlet-ver> <mtecc-ver> <humanizer-sha>");
    return 1;
}

var coverletXml  = args[0];
var mteccXml     = args[1];
var latestJson   = args[2];
var historyJson  = args[3];
var feedXml      = args[4];
var coverletVer  = args[5];
var mteccVer     = args[6];
var humanizerSha = args[7];

Console.WriteLine($"[Compare] Coverlet XML  : {coverletXml}");
Console.WriteLine($"[Compare] MTECC XML     : {mteccXml}");
Console.WriteLine($"[Compare] Latest JSON   : {latestJson}");
Console.WriteLine($"[Compare] History JSON  : {historyJson}");
Console.WriteLine($"[Compare] Feed XML      : {feedXml}");

var jsonOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};

// Load existing history if present.
var existingHistory = new HistoryFile();
if (File.Exists(historyJson))
{
    try
    {
        existingHistory = JsonSerializer.Deserialize<HistoryFile>(File.ReadAllText(historyJson), jsonOpts)
            ?? new HistoryFile();
    }
    catch
    {
        Console.WriteLine("[Compare] Could not parse existing history.json - starting fresh");
    }
}

// Backfill per-run deltas for existing history entries if missing (covers older runs)
foreach (var run in existingHistory.Runs)
{
    try
    {
        // Only compute if both tool summaries have values and deltas are zero
        if (run != null && (run.LineDelta == 0 && run.BranchDelta == 0 && run.MethodDelta == 0))
        {
            // If line rates are non-zero (or both zero but present), compute delta
            run.LineDelta = Math.Round(run.Coverlet.LineRate - run.Mtecc.LineRate, 4);
            run.BranchDelta = Math.Round(run.Coverlet.BranchRate - run.Mtecc.BranchRate, 4);
            run.MethodDelta = Math.Round(run.Coverlet.MethodRate - run.Mtecc.MethodRate, 4);
        }
    }
    catch
    {
        // Ignore malformed historic entries
    }
}

var coverletReport = CoberturaParser.Parse(coverletXml, "coverlet");
var mteccReport      = CoberturaParser.Parse(mteccXml, "mtecc");

var (output, history) = ReportBuilder.Build(
    coverletReport, mteccReport, coverletVer, mteccVer, humanizerSha, existingHistory);

var feed = FeedBuilder.Build(history);

Directory.CreateDirectory(Path.GetDirectoryName(latestJson)!);
Directory.CreateDirectory(Path.GetDirectoryName(historyJson)!);
Directory.CreateDirectory(Path.GetDirectoryName(feedXml)!);

File.WriteAllText(latestJson,  JsonSerializer.Serialize(output,  jsonOpts));
File.WriteAllText(historyJson, JsonSerializer.Serialize(history, jsonOpts));
feed.Save(feedXml);

Console.WriteLine($"[Compare] Written: {latestJson}");
Console.WriteLine($"[Compare] Written: {historyJson}");
Console.WriteLine($"[Compare] Written: {feedXml}");
return 0;
