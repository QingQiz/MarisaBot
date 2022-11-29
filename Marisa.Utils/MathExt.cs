namespace Marisa.Utils;

public static class MathExt
{
    // https://github.com/Monodesu/KanonBot/blob/f0f6bb1edcc4e460da6550a5626c5796441bf007/src/Utils.cs#L63
    public static double Log1P(double x)
        => Math.Abs(x) > 1e-4 ? Math.Log(1.0 + x) : (-0.5 * x + 1.0) * x;

    // ReSharper disable once MemberCanBePrivate.Global
    public const double EulerGamma = 0.57721566490153286060651209008240243104215933593992;

    // ReSharper disable once MemberCanBePrivate.Global
    public static Func<double, double> ExtremeValueDistribution(double alpha, double beta)
        => x => Math.Exp(-Math.Exp((-x + alpha) / beta) + (-x + alpha) / beta) / beta;

    public static (double alpha, double beta) FitExtremeValueDistribution(double[] data)
    {
        var n = data.Length;
        var mean = data.Average();
        var variance = data.Select(x => (x - mean) * (x - mean)).Sum() / n;
        var beta = Math.Sqrt(variance * 6) / Math.PI;
        var alpha = mean - beta * EulerGamma;
        return (alpha, beta);
    }
}