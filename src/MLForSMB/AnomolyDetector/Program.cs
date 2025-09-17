using AnomolyDetector.Models;
using System.Globalization;

var input = "Data/input.csv";
var output = "Data/anomalies.csv";
var threshold = 0.35;
var batchSize = 64;
var sensitivity = 95;

if (!File.Exists(input))
{
    Console.Error.WriteLine($"Input not found: {input}");
    return;
}

var rows = LoadCsv(input);
if (rows.Count < 30)
{
    Console.Error.WriteLine($"Not enough input. There are {rows.Count} rows available, while 30 are required.");
    return;
}



static List<Input> LoadCsv(string path)
{
    var list = new List<Input>();
    using var sr = new StreamReader(path);
    var header = sr.ReadLine();
    
    while (!sr.EndOfStream)
    {
        var line = sr.ReadLine();
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
        {
            continue;
        }

        var parts = line.Split(',');
        if (parts.Length < 2)
        {
            continue;
        }

        if (!DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            continue;
        }

        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
        {
            continue;
        }

        list.Add(new Input(date, amount));
    }

    return list;
}