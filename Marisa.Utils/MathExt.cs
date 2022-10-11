namespace Marisa.Utils;

public static class MathExt
{
    // https://github.com/Monodesu/KanonBot/blob/f0f6bb1edcc4e460da6550a5626c5796441bf007/src/Utils.cs#L63
    public static double Log1P(double x)
        => Math.Abs(x) > 1e-4 ? Math.Log(1.0 + x) : (-0.5 * x + 1.0) * x;
}