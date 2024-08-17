using System.Numerics;
using Marisa.Plugin.Shared.Osu.Entity.Score;
using Marisa.Plugin.Shared.Util;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Path = System.IO.Path;

namespace Marisa.Plugin.Shared.Osu.Drawer;

public static class OsuScoreDrawer
{
    public static Image GetMiniCards(this List<(OsuScore, int)> score)
    {
        const int gap    = 5;
        const int margin = 20;

        var ims = score.Select(x => GetMiniCard(x.Item1)).ToList();

        var image =
            new Image<Rgba32>(margin * 2 + ims[0].Width, margin * 2 + ims.Count * ims[0].Height + (ims.Count - 1) * gap).Clear(Color.ParseHex("#382e32"));

        var drawY = margin;

        var font = OsuDrawerCommon.FontExo2.CreateFont(16);

        for (var i = 0; i < ims.Count; i++)
        {
            image.DrawImage(ims[i], margin, drawY);
            image.DrawText($"#{score[i].Item2 + 1}", font, Color.White, margin + 10, drawY + 5);
            drawY += ims[i].Height + gap;
        }

        return image;
    }

    private static Image GetMiniCard(this OsuScore score)
    {
        const int width   = 2000;
        const int height  = 100;
        const int marginX = 40;
        const int marginY = 10;
        const int gap     = 20;

        var im = new Image<Rgba32>(width, height).Clear(Color.FromRgb(84, 69, 76));

        var rankIcon = OsuDrawerCommon.GetRankIcon(score.Rank).ResizeY(50);
        {
            im.DrawImageVCenter(rankIcon, marginX);
        }

        {
            var font = OsuDrawerCommon.FontExo2.CreateFont(28);

            var opt = ImageDraw.GetTextOptions(font);

            opt.VerticalAlignment = VerticalAlignment.Bottom;
            opt.Origin            = new Vector2(marginX + rankIcon.Width + gap, height - marginY);

            im.DrawText(opt, score.Beatmap.Version, Color.FromRgb(238, 170, 0));

            var ago = score.CreatedAt.TimeAgo();

            opt.Origin = new Vector2(opt.Origin.X + score.Beatmap.Version.MeasureWithSpace(font).Width + gap, opt.Origin.Y + 5);

            im.DrawText(opt, ago, Color.FromRgb(163, 143, 152));
        }

        var rec = new Image<Rgba32>(200, height);
        {
            var pb = new PathBuilder();

            pb.AddLines(new PointF(0, 0), new PointF(rec.Width, 0), new PointF(rec.Width, rec.Height), new PointF(0, rec.Height),
                new PointF(20, (float)(rec.Height / 2.0))
            );

            rec.Mutate(i => i.Fill(Color.FromRgb(70, 57, 63), pb.Build()));

            var text1 = $"{score.Pp:N0}";
            var text2 = "pp";
            var font1 = OsuDrawerCommon.FontExo2.CreateFont(40, FontStyle.Bold);
            var font2 = OsuDrawerCommon.FontExo2.CreateFont(30);

            var w1 = text1.MeasureWithSpace(font1).Width;
            var w2 = text2.MeasureWithSpace(font2).Width;

            var x1 = 20 + (rec.Width - 20 - w1 - w2) / 2;
            var x2 = x1 + w1;

            rec.DrawTextVCenter(text1, font1, Color.FromRgb(255, 102, 171), (int)x1);
            rec.DrawTextVCenter(text2, font2, Color.FromRgb(209, 148, 175), (int)x2);

            im.DrawImage(rec, im.Width - rec.Width, 0);
        }

        {
            var truePp = $"{score.Weight?.Pp ?? 0:N0}pp";

            var font = OsuDrawerCommon.FontExo2.CreateFont(40, FontStyle.Bold);

            var opt = ImageDraw.GetTextOptions(font);

            opt.HorizontalAlignment = HorizontalAlignment.Right;
            opt.Origin              = new Vector2(width - rec.Width - gap, (height - truePp.MeasureWithSpace(font).Height) / 2);

            im.DrawText(opt, truePp, Color.White);
        }

        {
            var acc    = $"{score.Accuracy * 100:F2}%";
            var weight = $"权重：{score.Weight?.Percentage ?? 0:F0}%";

            var font1 = OsuDrawerCommon.FontExo2.CreateFont(33, FontStyle.Bold);
            var font2 = OsuDrawerCommon.FontYaHei.CreateFont(28);

            var opt = ImageDraw.GetTextOptions(font1);

            opt.Origin = new Vector2(1460, marginY);

            im.DrawText(opt, acc, Color.ParseHex("#FFCC22"));

            opt.Font              = font2;
            opt.Origin            = opt.Origin with { Y = height - marginY };
            opt.VerticalAlignment = VerticalAlignment.Bottom;

            im.DrawText(opt, weight, Color.White);
        }

        var modIconDrawX = 1430;
        {
            const int modIconWidth = 80;
            const int iconGap      = 10;

            var icons = score.Mods.Select(OsuModDrawer.GetModIconWithoutText);

            foreach (var i in icons)
            {
                var draw = i.ResizeX(modIconWidth);
                modIconDrawX -= draw.Width;

                im.DrawImageVCenter(draw, modIconDrawX);
                modIconDrawX -= iconGap;
            }
        }

        {
            var font = OsuDrawerCommon.FontExo2.CreateFont(35);

            var text = score.Beatmapset.TitleUnicode + " by " + score.Beatmapset.ArtistUnicode;

            while (text.MeasureWithSpace(font).Width + marginX + rankIcon.Width + gap > modIconDrawX)
            {
                text = text[..^4] + "...";
            }

            im.DrawText(text, font, Color.White, marginX + rankIcon.Width + gap, marginY);
        }


        return im.RoundCorners(15);
    }

    public static Image GetAccRing(string rank, double acc, int modeInt, bool withText = true)
    {
        var fontCollection = new FontCollection();

        var fontVenera = fontCollection.Add(Path.Join(OsuDrawerCommon.ResourcePath, "Venera-700.otf"));
        fontCollection.AddSystemFonts();

        var rankFont = fontVenera.CreateFont(120);

        var accRing = new Image<Rgba32>(400, 400);

        var gradeRingCenter = new Point(accRing.Width / 2, accRing.Height / 2);

        var rankIndex = modeInt switch
        {
            0 => new[] { 1, 0.99, 0.9333, 0.8667, 0.8, 0.6 },
            1 => new[] { 1, 0.99, 0.95, 0.9, 0.8 },
            _ => new[] { 1, 0.99, 0.95, 0.9, 0.8, 0.7 }
        };

        var rankRingColor = new[]
        {
            Color.ParseHex("#be0089"),
            Color.ParseHex("#0096a2"),
            Color.ParseHex("#72c904"),
            Color.ParseHex("#d99d03"),
            Color.ParseHex("#ea7948"),
            Color.ParseHex("#ff5858")
        };

        foreach (var (angle, i) in rankIndex.Select((angle, i) => (angel: angle, i)))
        {
            accRing.Mutate(im => im
                .Fill(rankRingColor[i], ShapeDraw.BuildRing(gradeRingCenter, 145, 10, (int)(360 * angle)))
            );
        }

        var accRingBrush = new LinearGradientBrush(
            new PointF(gradeRingCenter.X, 0), new PointF(gradeRingCenter.X, accRing.Height),
            GradientRepetitionMode.None,
            new ColorStop(0, Color.ParseHex("#66ccfe")), new ColorStop(1, Color.ParseHex("#b3ff67"))
        );

        accRing.Mutate(im => im
            .Fill(Color.Black, ShapeDraw.BuildRing(gradeRingCenter, accRing.Height / 2, 50, 360))
            .Fill(accRingBrush, ShapeDraw.BuildRing(gradeRingCenter, accRing.Height / 2, 50, (int)(360 * acc)))
        );

        if (!withText) return accRing;

        var im = new Image<Rgba32>(300, 300);

        rank = rank.Trim('h').Trim('H');
        im.DrawTextCenter(rank, rankFont, Color.White, 0, 25);
        im.Mutate(i => i.GaussianBlur(15));
        im.DrawTextCenter(rank, rankFont, Color.White, 0, 25);

        accRing.DrawImageCenter(im);

        return accRing;
    }
}