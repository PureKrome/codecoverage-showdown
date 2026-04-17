using System.Text.Json;
using System.Text.Json.Serialization;
using CoverageCompare;
using CoverageCompare.Models;

// Args:
//   <coverlet-cobertura.xml>
//   <mtp-cobertura.xml>
//   <output-latest.json>
//   <output-history.json>
//   <coverlet-version>
//   <mtp-version>
//   <humanizer-sha>

if (args.Length < 7)
{
    Console.Error.WriteLine(
        "Usage: CoverageCompare <coverlet.xml> <mtp.xml> <latest.json> <history.json> " +
        "<coverlet-ver> <mtp-ver> <humanizer-sha>");
    return 1;
}

var coverletXml  = args[0];
var mtpXml       = args[1];
var latestJson   = args[2];
var historyJson  = args[3];
var coverletVer  = args[4];
var mtpVer       = args[5];
var humanizerSha = args[6];

Console.WriteLine($"[Compare] Coverlet XML  : {coverletXml}");
Console.WriteLine($"[Compare] MTP XML       : {mtpXml}");
Console.WriteLine($"[Compare] Latest JSON   : {latestJson}");
Console.WriteLine($"[Compare] History JSON  : {historyJson}");

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

Directory.CreateDirectory(Path.GetDirectoryName(latestJson)!);
Directory.CreateDirectory(Path.GetDirectoryName(historyJson)!);

File.WriteAllText(latestJson,  JsonSerializer.Serialize(output,  jsonOpts));
File.WriteAllText(historyJson, JsonSerializer.Serialize(history, jsonOpts));

Console.WriteLine($"[Compare] Written: {latestJson}");
Console.WriteLine($"[Compare] Written: {historyJson}");
return 0;
