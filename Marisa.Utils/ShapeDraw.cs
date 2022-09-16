using SixLabors.ImageSharp;

namespace Marisa.Utils;

public static class ShapeDraw
{
    public static List<PointF> BuildHexagon(PointF center, int r)
    {
        var d = 0.0;

        var points = new List<PointF>();

        for (var i = 0; i <= 6; i++)
        {
            var x = r * Math.Cos(d) + center.X;
            var y = r * Math.Sin(d) + center.Y;

            points.Add(new PointF((float)x, (float)y));

            d += Math.PI / 3;
        }

        return points;
    }
}