using DemandForecaster.Models;
using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using System.Globalization;

// Forecasting demand.
var input = "Data/sales.csv";
var horizon = 14;
var windowSize = 7;
var seriesLength = 60;
var trainSize = 90;

var sku = ParseSku(args);
if (string.IsNullOrWhiteSpace(sku))
{
    Console.Error.WriteLine("SKU not specified. Use --sku <SKU> to specify the SKU.");
    return;
}

if (!File.Exists(input))
{
    Console.Error.WriteLine($"Input not found: {input}");
    return;
}

var rows = LoadCsv(input);
rows = [.. rows.Where(r => r.SKU == sku)];

if (rows.Count < Math.Max(trainSize, 30))
{
    Console.Error.WriteLine($"Not enough sales. There are {rows.Count} sales available, while {Math.Max(trainSize, 30)} are required.");
    return;
}

rows = [.. rows.OrderBy(r => r.Date)];

var ml = new MLContext(seed: 42);
var dataView = ml.Data.LoadFromEnumerable(rows.Select(r => new TimePoint { Units = r.Units }));

var pipeline = ml.Forecasting.ForecastBySsa(
    outputColumnName: nameof(ForecastPoint.ForecastedValues),
    inputColumnName: nameof(TimePoint.Units),
    windowSize: windowSize,
    seriesLength: seriesLength,
    trainSize: trainSize,
    horizon: horizon,
    confidenceLevel: 0.95f,
    confidenceLowerBoundColumn: nameof(ForecastPoint.LowerBoundValues),
    confidenceUpperBoundColumn: nameof(ForecastPoint.UpperBoundValues));    

var model = pipeline.Fit(dataView);
var engine = model.CreateTimeSeriesEngine<TimePoint, ForecastPoint>(ml);

var forecast = engine.Predict();
var startDate = rows.Last().Date.AddDays(1);

Console.WriteLine($"Forecast for next {horizon} days");
for (var i = 0; i < horizon; i++)
{ 
    var date = startDate.AddDays(i).ToString("yyyy-MM-dd");
    Console.WriteLine($"{date}: {forecast.ForecastedValues[i]:0.##} (95% CI: {forecast.LowerBoundValues[i]:0.##}–{forecast.UpperBoundValues[i]:0.##})");
}

// Backtest the forecast
var (mae, mape) = Backtest(ml, rows, windowSize, seriesLength, trainSize, horizon);
Console.WriteLine($"Backtest (last {horizon} days): Mean Absolute Error={mae:0.##}, Mean Absolute Percentage Error={mape:P2}");

// Smart staffing
var staffCapacityPerDay = 40;
for (int i = 0; i < horizon; i++)
{
    var demandForecast = forecast.ForecastedValues[i];
    var staffNeeded = (int)Math.Ceiling(demandForecast / staffCapacityPerDay);
    var date = startDate.AddDays(i).ToString("yyyy-MM-dd");
    Console.WriteLine($"{date}: Expected {demandForecast:0.##} orders → {staffNeeded} staff needed");
}

static (double MAE, double MAPE) Backtest(MLContext ml, List<Sale> rows, int window, int series, int train, int horizon)
{
    var n = rows.Count;
    var h = Math.Min(horizon, Math.Max(1, n / 10)); 
    var trainRows = rows.Take(n - h).ToList();
    var testRows = rows.Skip(n - h).ToList();

    var dataViewTrain = ml.Data.LoadFromEnumerable(trainRows.Select(r => new TimePoint { Units = r.Units }));
    var pipeline = ml.Forecasting.ForecastBySsa(
        outputColumnName: nameof(ForecastPoint.ForecastedValues),
        inputColumnName: nameof(TimePoint.Units),
        windowSize: window,
        seriesLength: series,
        trainSize: Math.Min(train, Math.Max(2, trainRows.Count - 1)),
        horizon: h,
        confidenceLevel: 0.95f,
        confidenceLowerBoundColumn: nameof(ForecastPoint.LowerBoundValues),
        confidenceUpperBoundColumn: nameof(ForecastPoint.UpperBoundValues));

    var model = pipeline.Fit(dataViewTrain);
    var engine = model.CreateTimeSeriesEngine<TimePoint, ForecastPoint>(ml);
    var prediction = engine.Predict();

    var mae = 0.0;
    var mape = 0.0;
    for (var i = 0; i < h; i++)
    {
        var y = testRows[i].Units;
        var yhat = prediction.ForecastedValues[i];
        mae += Math.Abs(yhat - y);
        if (y != 0)
        {
            mape += Math.Abs((yhat - y) / y);
        }
    }

    mae /= h; mape /= h;
    
    return (mae, mape);
}

static List<Sale> LoadCsv(string path)
{
    var list = new List<Sale>();
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
        if (parts.Length < 3)
        {
            continue;
        }

        var date = DateTimeOffset.Parse(parts[0], CultureInfo.InvariantCulture);
        var sku = parts[1];
        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var units))
        {
            continue;
        }

        list.Add(new Sale(date, units, sku));
    }

    return list;
}

static string ParseSku(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--sku")
        {
            return args[i + 1];
        }
    }

    return string.Empty;
}