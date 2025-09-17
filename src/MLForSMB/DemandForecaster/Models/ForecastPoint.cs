namespace DemandForecaster.Models;

internal sealed class ForecastPoint
{
    public float[] ForecastedValues { get; set; } = default!;

    public float[] LowerBoundValues { get; set; } = default!;

    public float [] UpperBoundValues { get; set; } = default!;
}
