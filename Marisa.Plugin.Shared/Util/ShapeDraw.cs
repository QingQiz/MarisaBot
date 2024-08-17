using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;

namespace Marisa.Plugin.Shared.Util;

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

    public static IPath BuildRing(PointF center, int radius, int width, int angle)
    {
        var pb = new PathBuilder();

        if (angle >= 360)
        {
            pb.AddArc(center, radius, radius, 0, 0, angle);
            pb.AddArc(center, radius - width, radius - width, 0, angle, -angle);
        }
        else
        {
            var angle2 = 360 - angle + 90;
            var radian = angle2 * Math.PI / 180;

            var point1 = new PointF(
                (float)(center.X + radius * Math.Cos(radian)),
                (float)(center.Y - radius * Math.Sin(radian))
            );

            var point2 = new PointF(
                (float)(center.X + (radius - width) * Math.Cos(radian)),
                (float)(center.Y - (radius - width) * Math.Sin(radian))
            );

            pb.AddArc(center, radius, radius, -90, 0, angle);
            pb.AddLine(point1, point2);
            pb.AddArc(center, radius - width, radius - width, -90, angle, -angle);
            pb.AddLine(center.X, 0, center.X, width);
        }

        pb.CloseFigure();

        return pb.Build();
    }
}