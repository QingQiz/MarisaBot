using Marisa.Plugin.Shared.Osu.Entity.Score;
using Marisa.Utils;
using Marisa.Utils.Cacheable;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Plugin.Shared.Osu.Drawer;

public static class OsuBeatmapDrawer
{
    public static Image GetBeatmapStatusIcon(string status)
    {
        status = status.ToLower();

        var im = new Image<Rgba32>(60, 60);

        switch (status)
        {
            case "ranked":
            {
                var font = OsuDrawerCommon.FontExo2.CreateFont(80, FontStyle.Bold);

                im.DrawTextCenter("‹‹", font, Color.ParseHex("#64c6f5"), withSpace: false);
                im.Mutate(i => i.Rotate(90));
                break;
            }
            case "loved":
            {
                var font = OsuDrawerCommon.FontIcon.CreateFont(70, FontStyle.Regular);

                im.DrawTextCenter("♥", font, Color.FromRgb(255, 102, 171), withSpace: false);
                break;
            }
            case "approved" or "qualified":
            {
                var font = OsuDrawerCommon.FontYaHei.CreateFont(50);

                im.DrawTextCenter("✔", font, Color.Black, withSpace: false);
                break;
            }
            default:
            {
                var font = OsuDrawerCommon.FontYaHei.CreateFont(45);

                im.DrawTextCenter("❔", font, Color.Black, withSpace: false);
                break;
            }
        }

        return im;
    }

    public static Color GetStarRatingColor(double starRating)
    {
        if (starRating > 10) starRating = 10;
        if (starRating < 0) starRating  = 0;

        var starRatingColorGradiant = new CacheableImage(Path.Join(OsuDrawerCommon.TempPath, "StarRatingColorGradiant.png"), () =>
        {
            var brush = new LinearGradientBrush(
                new PointF(0, 0), new PointF(2000, 0),
                GradientRepetitionMode.None,
                new ColorStop(0, Color.ParseHex("#4290ff")),
                new ColorStop(0.20f, Color.ParseHex("#4fc0ff")),
                new ColorStop(0.27f, Color.ParseHex("#7cff4f")),
                new ColorStop(0.35f, Color.ParseHex("#f6f05c")),
                new ColorStop(0.50f, Color.ParseHex("#ff4e6f")),
                new ColorStop(0.65f, Color.ParseHex("#c645b8")),
                new ColorStop(0.75f, Color.ParseHex("#6563de")),
                new ColorStop(0.90f, Color.ParseHex("#12106d")),
                new ColorStop(1.00f, Color.ParseHex("#000000"))
            );

            var res = new Image<Rgba32>(2000, 50);
            res.Mutate(i => i.Fill(brush));

            return res;
        }).Value.CloneAs<Rgba32>();

        var x = (int)(starRating / 10.0 * starRatingColorGradiant.Width);

        if (x == starRatingColorGradiant.Width) x--;

        return starRatingColorGradiant[x, starRatingColorGradiant.Height / 2];
    }

    public static Image GetBeatmapDetail(Beatmap beatmap)
    {
        return new CacheableImage(Path.Join(OsuDrawerCommon.TempPath, "BeatmapDetail-") + beatmap.Checksum + ".png", () =>
        {
            const int imageWidth = 500, margin = 20, padding = 10;

            // same to BgColor, but with transparent
            var bgColor     = Color.FromRgba(46, 53, 56, (byte)(0.2 * 255));
            var colorYellow = Color.ParseHex("#ffdd55");

            Image BeatmapCounter()
            {
                const int iconWidth = 40;

                var length  = TimeSpan.FromSeconds(beatmap.HitLength).ToString("m':'s");
                var bpm     = beatmap.Bpm.ToString();
                var circles = beatmap.CountCircles.ToString("N0");
                var sliders = beatmap.CountSliders.ToString("N0");

                var font = OsuDrawerCommon.FontExo2.CreateFont(20, FontStyle.Bold);

                var img = new Image<Rgba32>(imageWidth, iconWidth + margin * 2).Clear(bgColor);

                var step = (int)((imageWidth - margin * 2 - sliders.MeasureWithSpace(font).Width - padding - iconWidth) / 3);

                var x = margin;
                img.DrawImageVCenter(OsuDrawerCommon.GetIcon("total_length").ResizeX(iconWidth), x);
                img.DrawTextVCenter(length, font, colorYellow, x + iconWidth + padding);
                x += step;
                img.DrawImageVCenter(OsuDrawerCommon.GetIcon("bpm").ResizeX(iconWidth), x);
                img.DrawTextVCenter(bpm, font, colorYellow, x + iconWidth + padding);
                x += step;
                img.DrawImageVCenter(OsuDrawerCommon.GetIcon("count_circles").ResizeX(iconWidth), x);
                img.DrawTextVCenter(circles, font, colorYellow, x + iconWidth + padding);
                x += step;
                img.DrawImageVCenter(OsuDrawerCommon.GetIcon("count_sliders").ResizeX(iconWidth), x);
                img.DrawTextVCenter(sliders, font, colorYellow, x + iconWidth + padding);

                return img;
            }

            Image BeatmapConfig()
            {
                const int barHeight      = 40;
                const int barHeightInner = 10;

                List<(string, double)> kv = new();

                switch (beatmap.ModeInt)
                {
                    case 3:
                    {
                        kv.AddRange(new[]
                        {
                            ("键位数量", beatmap.Cs),
                            ("掉血速度", beatmap.Drain),
                            ("准度要求", beatmap.Accuracy),
                            ("难度星级", beatmap.StarRating)
                        });
                        break;
                    }
                    case 0:
                    {
                        kv.AddRange(new[]
                        {
                            ("圆圈大小", beatmap.Cs),
                            ("掉血速度", beatmap.Drain),
                            ("准度要求", beatmap.Accuracy),
                            ("缩圈速度", beatmap.Ar),
                            ("难度星级", beatmap.StarRating)
                        });
                        break;
                    }
                    case 1:
                    {
                        kv.AddRange(new[]
                        {
                            ("掉血速度", beatmap.Drain),
                            ("准度要求", beatmap.Accuracy),
                            ("难度星级", beatmap.StarRating)
                        });
                        break;
                    }
                    case 2:
                    {
                        kv.AddRange(new[]
                        {
                            ("圆圈大小", beatmap.Cs),
                            ("掉血速度", beatmap.Drain),
                            ("准度要求", beatmap.Accuracy),
                            ("缩圈速度", beatmap.Ar),
                            ("难度星级", beatmap.StarRating)
                        });
                        break;
                    }
                }

                var img = new Image<Rgba32>(imageWidth, barHeight * kv.Count + margin * 2 + padding * (kv.Count - 1)).Clear(bgColor);

                var font = OsuDrawerCommon.FontYaHei.CreateFont(20);

                var (_, _, kWidth, kHeight) = kv[0].Item1.MeasureWithSpace(font);

                var vWidth   = kv.Max(x => x.Item2.ToString("0.##").MeasureWithSpace(font).Width);
                var barWidth = (int)(imageWidth - margin * 2 - padding * 2 - kWidth - vWidth);

                for (var i = 0; i < kv.Count; i++)
                {
                    var color = i == kv.Count - 1 ? colorYellow : Color.White;

                    var v  = kv[i].Item2.ToString("0.##");
                    var vM = v.MeasureWithSpace(font).Width;

                    var x2 = (int)(margin + kWidth + padding);
                    var x3 = (int)(imageWidth + kWidth + padding + barWidth + padding - vM) / 2;
                    var y1 = (int)(margin + i * (barHeight + padding) + (barHeight - kHeight) / 2);
                    var y2 = margin + i * (barHeight + padding) + (barHeight - barHeightInner) / 2;

                    img.DrawText(kv[i].Item1, font, Color.White, margin, y1);

                    var rect1 = new Rectangle(x2, y2, barWidth, barHeightInner);
                    var rect2 = new Rectangle(x2, y2, (int)(barWidth * (Math.Min(kv[i].Item2, 10.0) / 10)), barHeightInner);

                    img.Mutate(x => x.Fill(Color.Black, rect1).Fill(color, rect2));
                    img.DrawText(v, font, Color.White, x3, y1);
                }

                return img;
            }

            var im1 = BeatmapCounter();
            var im2 = BeatmapConfig();

            var im = new Image<Rgba32>(imageWidth, im1.Height + 3 + im2.Height);
            im.DrawImage(im1, 0, 0);
            im.DrawImage(im2, 0, im1.Height + 3);

            return im;
        }).Value;
    }
}