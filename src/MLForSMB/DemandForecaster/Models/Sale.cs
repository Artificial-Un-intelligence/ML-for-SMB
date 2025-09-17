namespace DemandForecaster.Models;

internal sealed record Sale(
    DateTimeOffset Date, 
    float Units, 
    string SKU);
