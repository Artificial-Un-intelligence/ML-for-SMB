using Microsoft.ML.Data;

namespace AnomolyDetector.Models;

internal sealed class Output
{
    // Output schema: [IsAnomaly, RawScore, Magnitude]
    [VectorType(3)]
    public double[] Predictions { get; set; } = default!;
}
