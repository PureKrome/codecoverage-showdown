using System.Text.Json;
using System.Text.Json.Serialization;
using CoverageCompare;
using CoverageCompare.Models;

// Args:
//   <coverlet-cobertura.xml>
//   <mtp-cobertura.xml>
//   <output-latest.json>
//   <output-history.json>
//   <output-feed.xml>
//   <coverlet-version>
//   <mtp-version>
//   <humanizer-sha>

if (args.Length < 8)
{
    Console.Error.WriteLine(
        "Usage: CoverageCompare <coverlet.xml> <mtp.xml> <latest.json> <history.json> <feed.xml> " +
        "<coverlet-ver> <mtp-ver> <humanizer-sha>");
    return 1;
}

var coverletXml  = args[0];
var mtpXml       = args[1];
var latestJson   = args[2];
var historyJson  = args[3];
var feedXml      = args[4];
var coverletVer  = args[5];
var mtpVer       = args[6];
var humanizerSha = args[7];

Console.WriteLine($"[Compare] Coverlet XML  : {coverletXml}");
Console.WriteLine($"[Compare] MTP XML       : {mtpXml}");
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

var coverletReport = CoberturaParser.Parse(coverletXml, "coverlet");
var mtpReport      = CoberturaParser.Parse(mtpXml, "mtp");

var (output, history) = ReportBuilder.Build(
    coverletReport, mtpReport, coverletVer, mtpVer, humanizerSha, existingHistory);

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
